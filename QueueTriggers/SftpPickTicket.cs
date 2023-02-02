using Microsoft.Azure.WebJobs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Pgd.Wsi.Data;
using Pgd.Wsi.Models.PickTicket;
using Pgd.Wsi.Models;
using Renci.SshNet;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pgd.Wsi.QueueTriggers
{
    public class SftpPickTicket
    {
        private readonly string _cs;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly HttpClient _magento;
        private readonly SftpClient _sftp;

        public SftpPickTicket(IHttpClientFactory httpClientFactory, 
            ConnectionInfo connectionInfo, 
            SqlConnectionStringBuilder builder,
            JsonSerializerOptions jsonOptions)
        {
            _magento = httpClientFactory.CreateClient("magento");
            _sftp = new(connectionInfo);
            _cs = builder.ConnectionString;
            _jsonOptions = jsonOptions;
        }

        [FunctionName("SftpPickTicket")]
        public async Task Run([QueueTrigger("sftp-pt", Connection = "AzureWebJobsStorage")]string pickTicketNumber, ILogger log)
        {
            log.LogInformation($"Creating a CSV for {pickTicketNumber}");
            PickTicketModel pickTicket = PickTicket.GetPickTicket(pickTicketNumber, _cs);

            StringBuilder csv = new();
            GeneratePickTicketHeader(pickTicket, csv);

            foreach (PickTicketDetailModel lineItem in pickTicket.LineItems) 
            {
                await GenerateOrderDetail(pickTicketNumber, lineItem, csv);
            }

            try
            {
                _sftp.Connect();
                _sftp.ChangeDirectory("Inbound");

                Stream csvContent = new MemoryStream();
                StreamWriter writer = new(csvContent);
                writer.Write(csv);
                writer.Flush();

                csvContent.Position = 0;
                log.LogInformation($"Starting SFTP for PT_WSI_{DateTime.Now:MM_dd_yyyy_HH_mm_ss_fffff}.csv");
                _sftp.UploadFile(csvContent, $"PT_WSI_{DateTime.Now:MM_dd_yyyy_HH_mm_ss_fffff}.csv");
            }
            catch
            {
                throw;
            }
            finally
            {
                _sftp.Disconnect();
            }
        }

        /// <summary>
        /// Generates a header CSV record in WSI's specified format with a new line terminator
        /// </summary>
        /// <param name="order">Order to generate the header for</param>
        /// <returns>CSV record with a new line terminator</returns>
        private static void GeneratePickTicketHeader(PickTicketModel order, StringBuilder csv)
        {
            csv.Append($"PTH,I,{order.PickTicketNumber},{order.OrderNumber},C,");
            csv.Append($"{order.OrderDate.ToString("MM/dd/yyyy")},");
            csv.Append(new string(',', 3));
            csv.Append("75,");
            csv.Append(new string(',', 2));

            csv.Append($"\"{order.Customer.Name}\",");
            csv.Append($"\"{order.Customer.Street}\",");
            csv.Append($"\"{order.Customer.City}\",");
            csv.Append($"{order.Customer.State},");
            csv.Append($"{order.Customer.Country},");
            csv.Append($"{order.Customer.Zip},,");

            csv.Append($"\"{order.Recipient.Name}\",");
            csv.Append($"\"{order.Recipient.Street}\",");
            csv.Append($"\"{order.Recipient.City}\",");
            csv.Append($"{order.Recipient.State},");
            csv.Append($"{order.Recipient.Country},");
            csv.Append($"{order.Recipient.Zip}{new string(',', 8)}");

            csv.Append(order.ShippingMethod + new string(',', 3));
            csv.Append("PGD,,HN,PGD,PP");
            csv.Append(new string(',', 6));
            csv.Append('Y' + new string(',', 4));
            csv.Append("PT" + new string(',', 12));
            csv.AppendLine();
        }

        /// <summary>
        /// Generates a series of CSV detail records in WSI's specified format with a new line terminator
        /// </summary>
        /// <param name="order">Order to generate detail records for</param>
        /// <returns>CSV records separated by new line terminators</returns>
        private async Task GenerateOrderDetail(string pickTicketNumber, PickTicketDetailModel lineItem, StringBuilder csv)
        {
            csv.Append("PTD,I,");
            csv.Append($"{pickTicketNumber},");
            csv.Append($"{lineItem.LineNumber},A,");
            csv.Append($"{lineItem.Sku}{new string(',', 5)}");
            csv.Append($"{lineItem.Units},{lineItem.Units}{new string(',', 3)}");

            HttpResponseMessage response = await _magento.GetAsync($"/api/products/{lineItem.Sku}");
            response.EnsureSuccessStatusCode();
            HttpContent content = response.Content;

            MagentoProduct product = JsonSerializer.Deserialize<MagentoProduct>(await content.ReadAsStringAsync(), _jsonOptions);

            csv.Append($"{product.Price}{new string(',', 3)}");
            csv.Append($"HN,PGD{new string(',', 8)}");
            csv.AppendLine();
        }
    }
}
