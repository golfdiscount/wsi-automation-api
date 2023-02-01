using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web.Http;
using Pgd.Wsi.Data;
using Pgd.Wsi.Models;
using Pgd.Wsi.Services;
using Pgd.Wsi.Models.PickTicket;
using Microsoft.Extensions.Primitives;

namespace Pgd.Wsi.HttpTriggers
{
    public class PickTickets
    {
        private readonly string _cs;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SftpService _wsiSftp;
        private readonly HttpClient _magento;
        private readonly HttpClient _duffers;
        private readonly ILogger _logger;

        public PickTickets(SqlConnectionStringBuilder builder,
            JsonSerializerOptions jsonOptions,
            SftpService wsiSftp,
            IHttpClientFactory httpClientFactory,
            ILoggerFactory logFactory)
        {
            _cs = builder.ConnectionString;
            _jsonOptions = jsonOptions;
            _wsiSftp = wsiSftp;
            _magento = httpClientFactory.CreateClient("magento");
            _duffers = httpClientFactory.CreateClient("dufferscorner");
            _logger = logFactory.CreateLogger(LogCategories.CreateFunctionUserCategory("Pgd.Wsi.HttpTriggers.Orders"));
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
            string requestContents = reader.ReadToEnd().Trim();

            PickTicketModel order = JsonSerializer.Deserialize<PickTicketModel>(requestContents, _jsonOptions);
            order.Channel = 2; // TODO: Remove channel hardcoding
            order.PickTicketNumber = "C" + order.OrderNumber;

            try
            {
                ValidateOrder(order);
                List<PickTicketModel> pickTickets = await SplitPickTicket(order);

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
                }

                string csv = await GeneratePickTicketCsv(pickTickets);

                if (csv.Length > 0)
                {
                    Stream fileContents = new MemoryStream();
                    StreamWriter writer = new(fileContents);
                    writer.Write(csv);
                    writer.Flush();

                    string fileName = $"PT_WSI_{DateTime.Now:MM_dd_yyyy_HH_mm_ss}.csv";

                    _logger.LogInformation($"Queuing {fileName} for SFTP");
                    _wsiSftp.Queue($"Inbound/{fileName}", fileContents);
                    int fileUploadCount = _wsiSftp.UploadQueue();
                    _logger.LogInformation($"Uploaded {fileUploadCount} file(s) to WSI");
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
        /// Splits a pick ticket into separate pick tickets if there are items that 
        /// qualify for expedited shipping
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
            List<PickTicketModel> pickTickets = new();

            List<PickTicketDetailModel> expeditedItems = new();
            PickTicketModel expeditedPickTicket = null;
            List<int> indicesToDelete = new();

            // Determine which line items qualify for expedited shipping
            for (int i = pickTicket.LineItems.Count - 1; i >= 0; i--)
            {
                PickTicketDetailModel lineItem = pickTicket.LineItems[i];

                if (expeditedSkus.Contains(lineItem.Sku))
                {
                    expeditedItems.Add(lineItem);
                    indicesToDelete.Add(i);
                }
            }

            // All line items qualify to be expedited
            if (pickTicket.LineItems.Count == expeditedItems.Count)
            {
                pickTicket.ShippingMethod = "FX2D";
            }
            // Only some line items qualify to be expedited
            else if (expeditedItems.Count > 0) // Some line items qualify to be expedited
            {
                // Remove qualifying items from original pick ticket
                indicesToDelete.ForEach(index =>
                {
                    pickTicket.LineItems.RemoveAt(index);
                });

                expeditedPickTicket = pickTicket.DeepClone();
                // Add suffix for uniqueness
                expeditedPickTicket.PickTicketNumber += "_WSIX";
                expeditedPickTicket.ShippingMethod = "FX2D";
                expeditedPickTicket.LineItems = expeditedItems;

                pickTickets.Add(expeditedPickTicket);
            }

            pickTickets.Add(pickTicket);

            return pickTickets;
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

        /// <summary>
        /// Generates a singular CSV for a a collection of pick tickets
        /// </summary>
        /// <param name="pickTickets">Collection of pick tickets</param>
        /// <returns>CSV containing pick ticket information</returns>
        private async Task<string> GeneratePickTicketCsv(List<PickTicketModel> pickTickets)
        {
            StringBuilder csv = new();

            foreach (PickTicketModel pickTicket in pickTickets)
            {
                GeneratePickTicketHeader(pickTicket, csv);

                foreach (PickTicketDetailModel lineItem in pickTicket.LineItems)
                {
                    await GenerateOrderDetail(pickTicket.PickTicketNumber, lineItem, csv);
                }
            }

            return csv.ToString();
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
