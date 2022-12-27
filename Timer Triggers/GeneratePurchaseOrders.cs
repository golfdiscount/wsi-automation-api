using Microsoft.Azure.WebJobs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WsiApi.Data;
using WsiApi.Models.PurchaseOrder;
using WsiApi.Services;

namespace WsiApi.Timer_Triggers
{
    public class GeneratePurchaseOrders
    {
        private readonly HttpClient duffersClient;
        private readonly string cs;
        private readonly SftpService _wsiSftp;
        public GeneratePurchaseOrders(IHttpClientFactory clientFactory, 
            SqlConnectionStringBuilder builder,
            SftpService wsiSftp)
        {
            duffersClient = clientFactory.CreateClient("dufferscorner");
            cs = builder.ConnectionString;
            _wsiSftp= wsiSftp;
        }

        [FunctionName("GeneratePurchaseOrders")]
        public async Task Run([TimerTrigger("0 0 17 * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogDebug($"Connecting to {duffersClient.BaseAddress}/media/WSI_PO.csv");
            HttpResponseMessage response = await duffersClient.GetAsync("media/WSI_PO.csv");
            string masterPos = await response.Content.ReadAsStringAsync();
            masterPos = masterPos.Trim().Replace("\"", "").Replace("\r", "");
            string[] masterPoRecords = masterPos.Split('\n');
            masterPoRecords = masterPoRecords[1..];

            log.LogInformation($"Found {masterPoRecords.Length} POs to be uploaded");

            if (masterPoRecords.Length == 0 )
            {
                return;
            }

            log.LogDebug($"Connecting to {duffersClient.BaseAddress}/media/wis_daily_po.csv");
            response = await duffersClient.GetAsync("media/wsi_daily_po.csv");
            string dailyPos = await response.Content.ReadAsStringAsync();
            dailyPos = dailyPos.Trim().Replace("\"", "").Replace("\r", "");
            string[] dailyPosRecords = dailyPos.Split('\n');
            dailyPosRecords = dailyPosRecords[1..];

            Dictionary<string, PurchaseOrderModel> purchaseOrders = new();
            Dictionary<string, StringBuilder> poRecords = new();

            foreach (string poRecord in masterPoRecords) // Each poRecord is a purchase order detail item
            {
                string[] poFields = poRecord.Split(',');
                string poNumber = poFields[3];
                
                if (dailyPosRecords.Contains(poNumber))
                {
                    if (!purchaseOrders.ContainsKey(poNumber))
                    {
                        poRecords[poNumber] = new();
                        poRecords[poNumber].AppendLine($"ROH,I,P,{poNumber}{new string(',', 7)}PGD,HN{new string(',', 10)}");

                        purchaseOrders[poNumber] = new()
                        {
                            PoNumber = poNumber,
                            LineItems = new()
                        };
                    }

                    string[] recordFields = poRecord.Split(',');

                    PurchaseOrderDetailModel detail = new()
                    {
                        PoNumber = poNumber,
                        LineNumber = Convert.ToInt32(recordFields[4]),
                        Sku = recordFields[5],
                        Units = Convert.ToInt32(recordFields[10])
                    };

                    purchaseOrders[poNumber].LineItems.Add(detail);

                }
            }

            foreach (string poNumber in purchaseOrders.Keys)
            {
                log.LogInformation($"Inserting PO {poNumber} into the database");
                PurchaseOrder.InsertPurchaseOrder(purchaseOrders[poNumber], cs);

                Stream fileContents = new MemoryStream();
                StreamWriter writer = new(fileContents);
                writer.Write(GeneratePurchaseOrderCsv(purchaseOrders[poNumber]));
                writer.Flush();

                log.LogInformation($"Queueing PO {poNumber} to be uploaded");
                _wsiSftp.Queue($"Inbound/RO_{poNumber}.csv", fileContents);
            }

            int uploadCount = _wsiSftp.UploadQueue();
            log.LogInformation($"Uploaded {uploadCount} POs to WSI");
        }

        private static string GeneratePurchaseOrderCsv(PurchaseOrderModel purchaseOrder)
        {
            StringBuilder poCsv = new();

            poCsv.AppendLine($"ROH,I,P,{purchaseOrder.PoNumber}{new string(',', 7)}PGD,HN{new string(',', 10)}");

            foreach (PurchaseOrderDetailModel lineItem in purchaseOrder.LineItems)
            {
                poCsv.Append($"ROD,I,P,");
                poCsv.Append($"{purchaseOrder.PoNumber},{lineItem.LineNumber},{lineItem.Sku}");
                poCsv.Append($"{new string(',', 5)}{lineItem.Units}{new string(',', 3)}EA");
                poCsv.Append($"{new string(',', 7)}PGD,HN{new string(',', 5)}");
                poCsv.AppendLine();
            }

            return poCsv.ToString();
        }
    }
}
