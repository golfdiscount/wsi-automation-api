using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;

namespace wsi_triggers.Blob_Triggers
{
    public class ProcessShippingConfirmationscs
    {
        private readonly HttpClient shipstation;
        private readonly JsonSerializerOptions jsonOptions;

        public ProcessShippingConfirmationscs(IHttpClientFactory clientFactory, JsonSerializerOptions jsonOptions)
        {
            shipstation = clientFactory.CreateClient("shipstation");
            this.jsonOptions = jsonOptions;
        }

        [FunctionName("ProcessShippingConfirmationscs")]
        public async Task Run([BlobTrigger("shipping-confirmations/{name}", Connection = "AzureWebJobsStorage")] Stream blob, string name, ILogger log)
        {
            log.LogInformation($"Processing shipping confirmations from {name}");

            StreamReader reader = new(blob);
            string csv = await reader.ReadToEndAsync();
            csv = csv.Trim();
            string[] records = csv.Split('\n');

            Dictionary<string, int> skuCounts = new();
            Dictionary<string, HashSet<string>> trackingNumbers = new();
            HashSet<string> failedOrders = new();

            foreach (string record in records)
            {
                string[] fields = record.Split(',');

                if (fields[0] == "CSD")
                {
                    string orderNumber = fields[6];
                    string sku = fields[14];
                    string trackingNumber = fields[32];
                    
                    if (skuCounts.ContainsKey(sku))
                    {
                        skuCounts[sku]++;
                    }
                    else
                    {
                        skuCounts[sku] = 1;
                    }

                    if (!trackingNumbers.ContainsKey(orderNumber))
                    {
                        trackingNumbers[orderNumber] = new();
                    }

                    trackingNumbers[orderNumber].Add(trackingNumber);
                }
            }

            foreach (string orderNumber in trackingNumbers.Keys)
            {
                Regex amazonPattern = new(@"\d+-\d+-\d+"); // This pattern checks to see if an order is an Amazon order
                string orderNumberQuery = amazonPattern.IsMatch(orderNumber) ? orderNumber : $"{orderNumber}_WSI"; // Amazon orders don't have _WSI at the end

                HttpResponseMessage response = await shipstation.GetAsync($"/orders?orderNumber={orderNumberQuery}");
                
                if (!response.IsSuccessStatusCode)
                {
                    log.LogError($"Unable to send request to ShipStation: {response.ReasonPhrase}");
                    failedOrders.Add(orderNumber);
                    break;
                }
                
                ShipstationOrderList orders = JsonSerializer.Deserialize<ShipstationOrderList>(response.Content.ReadAsStream(), jsonOptions);

                if (orders.Total == 0)
                {
                    log.LogWarning($"Query {orderNumberQuery} returned 0 results from ShipStation");
                    failedOrders.Add(orderNumber);
                    break;
                }

                ShipstationOrder order = null;

                foreach (ShipstationOrder shipstationOrder in orders.Orders)
                {
                    if (shipstationOrder.OrderNumber == orderNumberQuery)
                    {
                        order = shipstationOrder;
                    }
                }

                if (order == null)
                {
                    log.LogWarning($"Could not find an order number that matches {orderNumberQuery}");
                }

                foreach (string trackingNumber in trackingNumbers[orderNumber])
                {
                    JsonBody body = new()
                    {
                        OrderId = order.OrderId,
                        ShipDate = DateTime.Today.ToString("yyyy-MM-dd"),
                        TrackingNumber = trackingNumber
                    };

                    HttpResponseMessage postResponse = await shipstation.PostAsJsonAsync("/orders/markasshipped", body);

                    if (!postResponse.IsSuccessStatusCode)
                    {
                        log.LogError($"Unable to mark {orderNumber} as shipped in ShipStation");
                        failedOrders.Add(orderNumber);
                    }
                }
            }
        }

        private class JsonBody
        {
            public int OrderId { get; set; }
            public string CarrierCode { get; set; } = "fedex";
            public string ShipDate { get; set; }
            public string TrackingNumber { get; set; }
            public bool NotifyCustomer { get; set; } = true;
            public bool NotifySalesChannel { get; set; } = true;
        }
    
        private class ShipstationOrderList
        {
            public List<ShipstationOrder> Orders { get; set; }
            public int Total { get; set; }
            public int Page { get; set; }
            public int Pages { get; set; }  
        }

        private class ShipstationOrder
        {
            public int OrderId { get; set; }
            public string OrderNumber { get; set; }
        }
    }
}
