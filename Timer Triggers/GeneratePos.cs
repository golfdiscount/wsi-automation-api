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
using WsiApi.Models;
using WsiApi.Services;

namespace WsiApi.Timer_Triggers
{
    public class GeneratePos
    {
        private readonly HttpClient duffersClient;
        private readonly string cs;
        private readonly SftpService _wsiSftp;
        public GeneratePos(IHttpClientFactory clientFactory, 
            SqlConnectionStringBuilder builder,
            SftpService wsiSftp)
        {
            duffersClient = clientFactory.CreateClient("dufferscorner");
            cs = builder.ConnectionString;
            _wsiSftp= wsiSftp;
        }

        [FunctionName("GeneratePos")]
        public async Task Run([TimerTrigger("0 0 3 * * *")]TimerInfo myTimer, ILogger log)
        {
            HttpResponseMessage response = await duffersClient.GetAsync("media/WSI_PO.csv");
            string masterPos = await response.Content.ReadAsStringAsync();
            masterPos = masterPos.Trim().Replace("\"", "").Replace("\r", "");
            string[] masterPoRecords = masterPos.Split('\n');
            masterPoRecords = masterPoRecords[1..];

            response = await duffersClient.GetAsync("media/wsi_daily_po.csv");
            string dailyPos = await response.Content.ReadAsStringAsync();
            dailyPos = dailyPos.Trim().Replace("\"", "").Replace("\r", "");
            string[] dailyPosRecords = dailyPos.Split('\n');
            dailyPosRecords = dailyPosRecords[1..];

            Dictionary<string, StringBuilder> poRecords = new();

            foreach (string poRecord in masterPoRecords)
            {
                string[] poFields = poRecord.Split(',');
                string poNumber = poFields[3];
                
                if (dailyPosRecords.Contains(poNumber))
                {
                    if (!poRecords.ContainsKey(poNumber))
                    {
                        poRecords[poNumber] = new();
                        poRecords[poNumber].AppendLine($"ROH,I,P,{poNumber}{new string(',', 7)}PGD,HN{new string(',', 10)}");

                        PoHeaderModel header = new()
                        {
                            PoNumber = poNumber
                        };

                        PoHeaders.InsertHeader(header, cs);
                    }

                    string[] recordFields = poRecord.Split(',');

                    PoDetailModel detail = new()
                    {
                        PoNumber = poNumber,
                        LineNumber = Convert.ToInt32(recordFields[4]),
                        Sku = recordFields[5],
                        Units = Convert.ToInt32(recordFields[10])
                    };

                    PoDetails.InsertDetail(detail, cs);

                    poRecords[poNumber].AppendLine(poRecord);
                }
            }

            foreach (string poNumber in poRecords.Keys)
            {
                Stream fileContents = new MemoryStream();
                StreamWriter writer = new(fileContents);
                writer.Write(poRecords[poNumber]);
                writer.Flush();

                _wsiSftp.Queue($"Inbound/RO_{poNumber}.csv", fileContents);
            }

            int uploadCount = _wsiSftp.UploadQueue();
            log.LogInformation($"Uploaded {uploadCount} POs to WSI");
        }
    }
}
