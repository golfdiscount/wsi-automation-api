using Azure.Storage.Queues;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Pgd.Wsi.Models.SendGrid;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Pgd.Wsi.TimerTriggers
{
    public class ProcessShippingConfirmations
    {
        private readonly HttpClient shipstation;
        private readonly QueueServiceClient queueServiceClient;
        private readonly SftpClient wsiSftp;
        private readonly JsonSerializerOptions jsonOptions;

        private readonly List<string> recipients = new()
        {
            "harmeet@golfdiscount.com"
        };
        private readonly StringBuilder confirmationSummary = new(); // Summary of orders in format: order number, sku, tracking number
        private readonly Dictionary<string, int> skuCounts = new();
        private readonly Dictionary<string, HashSet<string>> trackingNumbers = new();
        private readonly HashSet<string> failedOrders = new();

        public ProcessShippingConfirmations(IHttpClientFactory clientFactory, 
            JsonSerializerOptions jsonOptions,
            QueueServiceClient queueServiceClient,
            ConnectionInfo sftpConnectionInfo)
        {
            shipstation = clientFactory.CreateClient("shipstation");
            this.jsonOptions = jsonOptions;
            this.queueServiceClient = queueServiceClient;
            this.wsiSftp = new(sftpConnectionInfo);
        }

        [FunctionName("ProcessShippingConfirmations")]
        public async Task Run([TimerTrigger("0 0 20 * * *")] TimerInfo timer, ILogger log)
        {
            string csv = GetShippingConfirmations();
            csv = csv.Trim();
            string[] records = csv.Split('\n');

            foreach (string record in records)
            {
                ProcessRecord(record);
            }

            foreach (string orderNumber in trackingNumbers.Keys)
            {
                try
                {
                    string orderKey = await MarkShipped(orderNumber);
                    log.LogInformation($"Marked {orderKey} as shipped in ShipStation");
                } catch (HttpRequestException e)
                {
                    log.LogError($"Unable to send request to ShipStation: {e.Message}");
                    failedOrders.Add(orderNumber);
                } catch (ArgumentException e)
                {
                    log.LogError(e.Message);
                    failedOrders.Add(orderNumber);
                }
            }

            QueueClient emailQueue = queueServiceClient.GetQueueClient("send-email");

            if (failedOrders.Count > 0)
            {
                log.LogError($"There were {failedOrders.Count} orders that were failed to be marked as shipped");

                SendGridMessageModel emailBody = new()
                {
                    To = recipients,
                    Subject = $"{failedOrders.Count} orders failed to be marked as shipped in ShipStation",
                    Body = $"The following orders could not be marked a shipped in ShipStation: {string.Join(',', failedOrders)}"
                };

                string emailBodyJson = JsonSerializer.Serialize(emailBody, jsonOptions);
                emailQueue.SendMessage(emailBodyJson);
            }

            StringBuilder skuCountCsv = new();

            foreach (string sku in skuCounts.Keys)
            {
                skuCountCsv.AppendLine($"{sku}, {skuCounts[sku]}");
            }

            DateTime today = DateTime.Now;
            SendGridMessageModel skuCountEmailBody = new()
            {
                To = recipients,
                Subject = $"{skuCounts.Keys.Count} SKU(s) were sent to WSI",
                Body = $"SKU counts for {today:MM-dd-yyyy}",
                Attachments = new()
            };

            Attachment skuCsvAttachment = new()
            {
                Filename = $"WSI_SKUs_{today:MM_dd_yyyy}.csv",
                Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(skuCountCsv.ToString())),
                Type = "text/csv",
            };

            Attachment confirmationSummaryAttachment = new()
            {
                Filename = $"Shipping Summary {today:MM-dd-yyyy}.csv",
                Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(confirmationSummary.ToString())),
                Type = "text/csv"
            };
            
            skuCountEmailBody.Attachments.Add(skuCsvAttachment);
            skuCountEmailBody.Attachments.Add(confirmationSummaryAttachment);
            string skuCountEmailBodyJson = JsonSerializer.Serialize(skuCountEmailBody, jsonOptions);
            emailQueue.SendMessage(skuCountEmailBodyJson);
        }

        private string GetShippingConfirmations()
        {
            StringBuilder csv = new();

            wsiSftp.Connect();
            List<SftpFile> dirFiles = wsiSftp.ListDirectory("Outbound").ToList();
            wsiSftp.Disconnect();

            DateTime now = DateTime.Now;
            Regex fileMask = new($"SC_[0-9]+_[0-9]+_{now:MMddyyyy}.+csv");

            List<SftpFile> shippingConfirmations = dirFiles.FindAll(file =>
            {
                return fileMask.IsMatch(file.Name);
            });

            foreach (SftpFile file in shippingConfirmations)
            {
                string[] lines = wsiSftp.ReadAllLines(file.FullName);
                    
                for (int i = 0; i < lines.Length; i++)
                {
                    csv.AppendLine(lines[i]);
                }
            }

            return csv.ToString();
        }

        private void ProcessRecord(string record)
        {
            string[] fields = record.Split(',');

            if (fields[0] == "CSD")
            {
                string orderNumber = fields[6];
                string sku = fields[14];
                string trackingNumber = fields[32];

                if (!skuCounts.ContainsKey(sku))
                {
                    skuCounts[sku] = 0;
                }

                skuCounts[sku] += int.Parse(fields[35]);

                if (!trackingNumbers.ContainsKey(orderNumber))
                {
                    trackingNumbers[orderNumber] = new();
                }

                trackingNumbers[orderNumber].Add(trackingNumber);

                confirmationSummary.AppendLine($"{orderNumber},{sku},{trackingNumber}");
            }
        }

    }
}
