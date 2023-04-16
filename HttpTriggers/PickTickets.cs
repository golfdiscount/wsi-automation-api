using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Pgd.Wsi.Data;
using Pgd.Wsi.Models.PickTicket;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web.Http;
using Azure.Storage.Queues;

namespace Pgd.Wsi.HttpTriggers
{
    public class PickTickets
    {
        private readonly string _cs;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly QueueClient _sftpQueue;
        private readonly HttpClient _magento;
        private readonly HttpClient _duffers;
        private readonly ILogger _logger;

        public PickTickets(SqlConnectionStringBuilder builder,
            JsonSerializerOptions jsonOptions,
            QueueServiceClient queueServiceClient,
            IHttpClientFactory httpClientFactory,
            ILoggerFactory logFactory)
        {
            _cs = builder.ConnectionString;
            _jsonOptions = jsonOptions;
            _sftpQueue = queueServiceClient.GetQueueClient("sftp-pt");
            _magento = httpClientFactory.CreateClient("magento");
            _duffers = httpClientFactory.CreateClient("dufferscorner");
            _logger = logFactory.CreateLogger(LogCategories.CreateFunctionUserCategory("Pgd.Wsi.HttpTriggers.PickTickets"));
        }

        [FunctionName("PickTickets")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "picktickets/{pickTicketNumber?}")] HttpRequest req,
            string pickTicketNumber)
        {
            if (req.Method == "POST")
            {
                return await Post(req, _logger);
            }

            if (pickTicketNumber != null)
            {
                return GetByPickTicketNumber(pickTicketNumber);
            }

            if (req.Query["orderNumber"] != StringValues.Empty)
            {
                return GetByOrderNumber(req.Query["orderNumber"]);
            }

            int page = (req.Query["page"] == StringValues.Empty) ? 1 : Convert.ToInt32(req.Query["page"]);
            int pageSize = (req.Query["pageSize"] == StringValues.Empty) ? 30 : Convert.ToInt32(req.Query["pageSize"]);

            return GetByPage(page, pageSize);
        }

        private IActionResult GetByPickTicketNumber(string pickTicketNumber)
        {
            _logger.LogInformation($"Searching database for pick ticket {pickTicketNumber}");
            PickTicketModel pickTicket = PickTicket.GetPickTicket(pickTicketNumber, _cs);

            if (pickTicket == null)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(pickTicket);
        } 

        private IActionResult GetByOrderNumber(string orderNumber)
        {
            _logger.LogInformation($"Searching database for pick tickets for order {orderNumber}");
            List<PickTicketModel> pickTickets = PickTicket.GetPickTicketByOrderNumber(orderNumber, _cs);

            if (pickTickets.Count == 0)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(pickTickets);
        }

        private IActionResult GetByPage(int page, int pageSize)
        {
            try
            {
                return new OkObjectResult(PickTicket.GetPickTicketByPage(page, pageSize, _cs));
            }
            catch (ArgumentException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }
        }

        /// <summary>
        /// Processes a post request for this route
        /// </summary>
        /// <param name="req">HttpRequest containing POST data</param>
        /// <param name="log">Logging object</param>
        /// <returns>A result indicating HTTP status of POST request</returns>
        private async Task<IActionResult> Post(HttpRequest req, ILogger log)
        {
            StreamReader reader = new(req.Body);
            string requestContents = (await reader.ReadToEndAsync()).Trim();

            PickTicketModel order = JsonSerializer.Deserialize<PickTicketModel>(requestContents, _jsonOptions);
            order.Channel = 2; // TODO: Remove channel hardcoding
            order.PickTicketNumber = "C" + order.OrderNumber;

            try
            {
                ValidateOrder(order);
                List<PickTicketModel> pickTickets = await SplitPickTicket(order);
                StringBuilder csv = new();

                foreach (PickTicketModel pickTicket in pickTickets)
                {
                    try
                    {
                        PickTicket.InsertPickTicket(pickTicket, _cs);
                        log.LogInformation($"Inserted pick ticket {pickTicket.PickTicketNumber} for order {pickTicket.OrderNumber}");
                    }
                    catch (SqlException ex)
                    {
                        // SQL Server error code 2627 corresponds to a primary key violation
                        if (ex.Number == 2627)
                        {
                            log.LogWarning($"Pick ticket {order.PickTicketNumber} already exists");
                            return new BadRequestErrorMessageResult($"Pick ticket {order.PickTicketNumber} already exists");
                        }

                        log.LogError(ex.Message);
                        return new InternalServerErrorResult();
                    }

                    await _sftpQueue.SendMessageAsync(pickTicket.PickTicketNumber);
                }

                return new StatusCodeResult(201);
            }
            catch (ValidationException ex)
            {
                log.LogWarning(ex.ValidationResult.ErrorMessage);
                return new BadRequestErrorMessageResult(ex.ValidationResult.ErrorMessage);
            }
        }

        /// <summary>
        /// Applies business rules and splits a pick ticket into multiple if necessary. Following
        /// business rules are applied:
        /// 
        /// <list type="bullet">
        ///     <item>Orders of 4 or less apparel items are shipped 2 day</item>
        ///     <item>Orders of 4 or less items with all items qualifying for 2 day are shipped 2 day</item>
        ///     <item>Orders of 5 or more items or orders that contain items not qualify for 2 day are split into
        ///         separate orders
        ///     </item>
        /// </list>
        /// </summary>
        /// <param name="pickTicket">Pick ticket to be split</param>
        /// <returns>A list of split pick tickets</returns>
        private async Task<List<PickTicketModel>> SplitPickTicket(PickTicketModel pickTicket)
        {
            // Get SKUs that qualify for free two day shipping
            HttpResponseMessage response = await _duffers.GetAsync("media/2day_skus.csv");
            response.EnsureSuccessStatusCode();

            string csvContents = await response.Content.ReadAsStringAsync();
            string[] records = csvContents.Trim().Replace("\"", string.Empty).Split("\r\n");
            // Remove header
            records = records[1..];
            HashSet<string> expeditedSkus = new(records);

            // Pick tickets generated after splitting process, may or may not contain new pick tickets
            List<PickTicketModel> splitPickTickets = new();
            // Mapping of pick tickets to the shipping method they qualify for
            Dictionary<PickTicketDetailModel, string> lineItems = new();

            pickTicket.LineItems.ForEach(lineItem =>
            {
                lineItems.Add(lineItem, pickTicket.ShippingMethod);
            });

            foreach (PickTicketDetailModel lineItem in lineItems.Keys)
            {
                // Item is a SKU that qualifies for free 2-day shipping
                if (expeditedSkus.Contains(lineItem.Sku))
                {
                    lineItems[lineItem] = "FX2D";
                } // Item is an apparel item and order line count is less than 4 qualifying it for free 2-day shipping
                else if (lineItem.Sku.Length == 10 && lineItems.Count <= 4)
                {
                    lineItems[lineItem] = "FX2D";
                }
            }

            foreach (string shippingMethod in lineItems.Values.Distinct())
            {
                PickTicketModel newPickTicket = pickTicket.DeepClone();
                newPickTicket.ShippingMethod = shippingMethod;
                newPickTicket.LineItems = lineItems.Where(kv => kv.Value == shippingMethod).Select(kv => kv.Key).ToList();

                // Shipping method is 2-day and there is more than 1 type of shipping method
                // Avoid changing order number if all items are part of one order
                if (shippingMethod == "FX2D" && lineItems.Values.Count > 1)
                {
                    newPickTicket.PickTicketNumber += "_WSIX";
                }

                for (int i = 0; i < newPickTicket.LineItems.Count; i++)
                {
                    newPickTicket.LineItems[i].LineNumber = i + 1;
                }

                splitPickTickets.Add(newPickTicket);
            };

            return splitPickTickets;
        }

        /// <summary>
        /// Validates an order to ensure that all data annotations are
        /// met
        /// </summary>
        /// <param name="order">Order to be validated</param>
        private static void ValidateOrder(PickTicketModel order)
        {
            ValidationContext validationContext = new(order);
            Validator.ValidateObject(order, validationContext, true);

            validationContext = new(order.Customer);
            Validator.ValidateObject(order.Customer, validationContext, true);

            validationContext = new(order.Recipient);
            Validator.ValidateObject(order.Recipient, validationContext, true);
        }
    }
}
