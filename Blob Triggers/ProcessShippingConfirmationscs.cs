using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace wsi_triggers.Blob_Triggers
{
    public class ProcessShippingConfirmationscs
    {
        [FunctionName("ProcessShippingConfirmationscs")]
        public async Task Run([BlobTrigger("shipping-confirmations/{name}", Connection = "AzureWebJobsStorage")] Stream blob, string name, ILogger log)
        {
            StreamReader reader = new(blob);
            string csv = await reader.ReadToEndAsync();
            csv = csv.Trim();
            string[] records = csv.Split('\n');

            Dictionary<string, int> skuCounts = new();

            foreach (string record in records)
            {
                string[] fields = record.Split(',');

                if (fields[0] == "CSD")
                {
                    if (skuCounts.ContainsKey(fields[14]))
                    {
                        skuCounts[fields[14]]++;
                    }
                    else
                    {
                        skuCounts[fields[14]] = 1;
                    }
                }
            }

            foreach (string sku in skuCounts.Keys)
            {
                log.LogInformation($"{sku},{skuCounts[sku]}");
            }
        }

        private class JsonBody
        {
            public string OrderId { get; set; }
            public string CarrierCode { get; set; } = "fedex";
            public DateOnly ShipDate { get; set; }
            public string TrackingNumber { get; set; }
            public bool NotifyCustomer { get; set; } = true;
            public bool NotifySalesChannel { get; set; } = true;
        }
    }
}
