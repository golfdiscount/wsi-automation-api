using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using wsi_triggers.Data;
using wsi_triggers.Models;
using wsi_triggers.Models.Address;
using wsi_triggers.Models.Detail;

namespace wsi_triggers.Queue_Triggers
{
    public class OrderCsvCreation
    {
        private readonly string cs;
        private readonly JsonSerializerOptions jsonOptions;
        private readonly HttpClient magento;
        public OrderCsvCreation(SqlConnectionStringBuilder builder, JsonSerializerOptions jsonOptions, IHttpClientFactory httpClientFactory)
        {
            cs = builder.ConnectionString;
            this.jsonOptions = jsonOptions;
            magento = httpClientFactory.CreateClient("magento");
        }

        [FunctionName("OrderCsvCreation")]
        public async Task Run([QueueTrigger("order-csv-creation", Connection = "AzureWebJobsStorage")]string orderNumber,
            ILogger log)
        {
            log.LogInformation($"Generating CSV for order {orderNumber}");

            using SqlConnection conn = new(cs);
            conn.Open();
            HeaderModel header = Headers.GetHeader(orderNumber, conn);

            if (header == null)
            {
                throw new ArgumentException($"{orderNumber} does not exist in the database");
            }

            List<GetDetailModel> details = Details.GetDetails(header.PickticketNumber, conn);

            StringBuilder orderCsv = new();
            orderCsv.AppendLine(GenerateHeader(header, conn));

            foreach(GetDetailModel detail in details)
            {
                try
                {
                    string detailCsv = await GenerateDetail(detail);
                    orderCsv.AppendLine(detailCsv);
                } catch (HttpRequestException)
                {
                    log.LogCritical("Unable to succesfully complete HTTP request");
                    throw;
                }               
            };

            QueueCsvSftp(orderCsv.ToString());
        }

        private static string GenerateHeader(HeaderModel header, SqlConnection conn)
        {
            StringBuilder headerCsv = new();
            headerCsv.Append($"PTH,{header.Action},{header.PickticketNumber},{header.OrderNumber},C,");
            headerCsv.Append($"{header.OrderDate.ToString("MM/dd/yyyy")},");
            headerCsv.Append(new string(',', 3));
            headerCsv.Append("75,");
            headerCsv.Append(new string(',', 2));

            AddressModel customer = Addresses.GetAddress(header.Customer, conn);

            headerCsv.Append($"\"{customer.Name}\",");
            headerCsv.Append($"\"{customer.Street}\",");
            headerCsv.Append($"\"{customer.City}\",");
            headerCsv.Append($"{customer.State},");
            headerCsv.Append($"{customer.Country},");
            headerCsv.Append($"{customer.Zip},,");

            AddressModel recipient = Addresses.GetAddress(header.Recipient, conn);

            headerCsv.Append($"\"{recipient.Name}\",");
            headerCsv.Append($"\"{recipient.Street}\",");
            headerCsv.Append($"\"{recipient.City}\",");
            headerCsv.Append($"{recipient.State},");
            headerCsv.Append($"{recipient.Country},");
            headerCsv.Append($"{recipient.Zip}{new string(',', 8)}");

            headerCsv.Append(header.ShippingMethod + new string(',', 3));
            headerCsv.Append("PGD,,HN,PGD,PP");
            headerCsv.Append(new string(',', 6));
            headerCsv.Append('Y' + new string(',', 4));
            headerCsv.Append("PT" + new string(',', 12));

            return headerCsv.ToString();
        }
    
        private async Task<string> GenerateDetail(DetailModel detail)
        {
            StringBuilder detailCsv = new();

            detailCsv.Append("PTD,I,");
            detailCsv.Append($"{detail.PickticketNumber},");
            detailCsv.Append($"{detail.LineNumber},A,");
            detailCsv.Append($"{detail.Sku}{new string(',', 5)}");
            detailCsv.Append($"{detail.Units},{detail.UnitsToShip}{new string(',', 3)}");

            HttpResponseMessage response = await magento.GetAsync($"/api/products/{detail.Sku}");
            response.EnsureSuccessStatusCode();
            HttpContent content = response.Content;

            MagentoProduct product = JsonSerializer.Deserialize<MagentoProduct>(await content.ReadAsStringAsync(), jsonOptions);

            detailCsv.Append($"{product.Price}{new string(',', 3)}");
            detailCsv.Append($"HN,PGD{new string(',', 8)}");

            return detailCsv.ToString();
        }
    
        private static void QueueCsvSftp(string csv)
        {
            BinaryData csvContents = new(csv);
            string fileName = $"PT_WSI_{DateTime.Now:MM_dd_yyyy_HH_mm_ss}.csv";
            BlobClient client = new(Environment.GetEnvironmentVariable("AzureWebJobsStorage"), "sftp", fileName);
            client.Upload(csvContents);
        }
    }
}
