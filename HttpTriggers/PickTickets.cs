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

            string csv;

            if (req.ContentType == "application/json")
            {
                PickTicketModel order = JsonSerializer.Deserialize<PickTicketModel>(requestContents, _jsonOptions);
                order.Channel = 2; // TODO: Remove channel hardcoding
                order.PickTicketNumber = "C" + order.OrderNumber;

                try
                {
                    ValidateOrder(order);
                    
                    PickTicket.InsertPickTicket(order, _cs);
                    log.LogInformation($"Inserted pick ticket {order.PickTicketNumber} for order {order.OrderNumber}");

                    csv = GenerateOrderHeader(order);

                    foreach (PickTicketDetailModel lineItem in order.LineItems)
                    {
                        csv += (await GenerateOrderDetail(order.PickTicketNumber, lineItem));
                        log.LogInformation($"Inserted line ${lineItem.LineNumber} for pick ticket {lineItem.LineNumber}");
                    }
                }
                catch (ValidationException ex)
                {
                    log.LogWarning(ex.ValidationResult.ErrorMessage);
                    return new BadRequestErrorMessageResult(ex.ValidationResult.ErrorMessage);
                }
                catch (SqlException ex)
                {
                    // SQL Server error code 2627 corresponds to a primary key violation
                    if (ex.Number == 2627)
                    {
                        log.LogInformation($"Pick ticket {order.PickTicketNumber} already exists");
                        return new BadRequestErrorMessageResult($"Pick ticket {order.PickTicketNumber} already exists");
                    }

                    log.LogError(ex.Message);
                    return new InternalServerErrorResult();
                }

            }
            else if (req.ContentType == "text/csv")
            {
                csv = await PostCsv(requestContents, log);
            }
            else
            {
                log.LogWarning("Incoming request did not have Content-Type header of application/json or text/csv");
                return new BadRequestErrorMessageResult("Request header Content-Type is not either application/json or text/csv");
            }

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

        /// <summary>
        /// Inserts orders in a CSV file into the database. If a pick ticket already exists, it is skipped.
        /// If a pick ticket contains SKUs that can be expedited, then it is split into 2 separate pick tickets.
        /// The output CSV may not always be the same as the input CSV due to this pick ticket splitting.
        /// </summary>
        /// <param name="csv">CSV file of orders.</param>
        /// <param name="log">Logging middleware.</param>
        /// <returns>CSV of inserted pick tickets.</returns>
        /// <exception cref="SqlException">Any SQL Server error other than a primary key violation.</exception>
        /// <expcetion cref="HttpRequestException">Occurs when method is unable to execute a 
        /// network request to get CSV file containing SKUs qualifying for expedited
        /// shipping from dufferscorner.</expcetion>
        private async Task<string> PostCsv(string csv, ILogger log)
        {
            // Get SKUs that qualify for free two day shipping
            HttpResponseMessage response = await _duffers.GetAsync("media/2day_skus.csv");
            response.EnsureSuccessStatusCode();

            string csvContents = await response.Content.ReadAsStringAsync();
            string[] records = csvContents.Trim().Replace("\"", string.Empty).Split("\r\n");
            // Remove header
            records = records[1..];

            HashSet<string> expeditedSkus = new(records);
            Dictionary<string, PickTicketModel> orders = ParseCsv(csv);
            StringBuilder orderCsv = new();

            // Split order and insert into database
            foreach (var orderKey in orders)
            {
                PickTicketModel order = orderKey.Value;
                List<PickTicketDetailModel> expeditedItems = new();
                PickTicketModel expeditedOrder = null;
                List<int> indicesToDelete = new();

                // Determine which line items qualify for expedited shipping
                for (int i = order.LineItems.Count - 1; i >= 0; i--)
                {
                    PickTicketDetailModel lineItem = order.LineItems[i];

                    if (expeditedSkus.Contains(lineItem.Sku))
                    {
                        expeditedItems.Add(lineItem);
                        indicesToDelete.Add(i);
                    }
                }

                // All line items qualify to be expedited
                if (order.LineItems.Count == expeditedItems.Count) 
                {
                    order.ShippingMethod = "FX2D";
                }
                // Only some line items qualify to be expedited
                else if (expeditedItems.Count > 0) // Some line items qualify to be expedited
                {
                    // Remove qualifying items
                    indicesToDelete.ForEach(index =>
                    {
                        order.LineItems.RemoveAt(index);
                    });

                    expeditedOrder = order.DeepClone();
                    expeditedOrder.LineItems = expeditedItems;
                }

                // Insert regular order into database
                try
                {
                    PickTicket.InsertPickTicket(order, _cs);
                    log.LogInformation($"Inserted {order.PickTicketNumber} for {order.OrderNumber}");
                    orderCsv.Append(GenerateOrderHeader(order));

                    foreach (PickTicketDetailModel lineItem in order.LineItems)
                    {
                        orderCsv.Append(await GenerateOrderDetail(lineItem.PickTicketNumber, lineItem));
                        log.LogInformation($"Inserted line {lineItem.LineNumber} for {lineItem.PickTicketNumber}");
                    }
                }
                catch (SqlException ex)
                {
                    // SQL Server error code 2627 corresponds to a primary key violation
                    if (ex.Number == 2627)
                    {
                        log.LogWarning($"{order.PickTicketNumber} already exists in DB, skipping...");
                    }
                    else
                    {
                        throw;
                    }
                }

                // Insert expedited order into database
                if (expeditedOrder != null)
                {
                    // Add suffix to order for uniqueness
                    expeditedOrder.PickTicketNumber += "-1";
                    expeditedOrder.ShippingMethod = "FX2D";
                    expeditedOrder.LineItems = expeditedItems;

                    try
                    {
                        PickTicket.InsertPickTicket(expeditedOrder, _cs);
                        log.LogInformation($"Inserted pick ticket {expeditedOrder.PickTicketNumber} for order {expeditedOrder.OrderNumber} into the database");
                        orderCsv.Append(GenerateOrderHeader(expeditedOrder));

                        foreach (PickTicketDetailModel lineItem in expeditedOrder.LineItems)
                        {
                            orderCsv.Append(await GenerateOrderDetail(expeditedOrder.PickTicketNumber, lineItem));
                            log.LogInformation($"Inserted line {lineItem.LineNumber} for {lineItem.PickTicketNumber}");
                        }
                    }
                    catch (SqlException ex) {
                        // SQL Server error code 2627 corresponds to a primary key violation
                        if (ex.Number == 2627)
                        {
                            log.LogWarning($"{order.PickTicketNumber} already exists in DB, skipping...");
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }

            return orderCsv.ToString();
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
        /// Parses a CSV containing pickticket headers and details
        /// </summary>
        /// <param name="csv">CSV to parse</param>
        /// <returns>A key value of pair of pick ticket number to an OrderModel</returns>
        private static Dictionary<string, PickTicketModel> ParseCsv(string csv)
        {
            Dictionary<string, PickTicketModel> orders = new();
            string[] records = csv.Split("\n");

            foreach (string record in records)
            {
                string[] fields = record.Split(',');
                string recordType = fields[0];
                string pickticketNum = fields[2];

                if (!orders.ContainsKey(pickticketNum))
                {
                    orders.Add(pickticketNum, new());
                    orders[pickticketNum].PickTicketNumber= pickticketNum;
                    orders[pickticketNum].LineItems = new();
                    orders[pickticketNum].Channel = 1;
                }

                PickTicketModel order = orders[pickticketNum];

                if (recordType == "PTH")
                {
                    order.OrderNumber = fields[3];
                    order.OrderDate = DateTime.ParseExact(fields[5], "MM/dd/yyyy", null);
                    order.Customer = new()
                    {
                        Name = fields[12].Replace("\"", ""),
                        Street = fields[13].Replace("\"", ""),
                        City = fields[14].Replace("\"", ""),
                        State = fields[15],
                        Country = fields[16],
                        Zip = fields[17]
                    };
                    order.Recipient = new()
                    {
                        Name = fields[19].Replace("\"", ""),
                        Street = fields[20].Replace("\"", ""),
                        City = fields[21].Replace("\"", ""),
                        State = fields[22],
                        Country = fields[23],
                        Zip = fields[24]
                    };
                    order.ShippingMethod = fields[32];
                    order.Store = 1;
                }
                else if (recordType == "PTD")
                {
                    order.LineItems.Add(new()
                    {
                        Sku = fields[5],
                        Units = int.Parse(fields[10]),
                        LineNumber = int.Parse(fields[3])
                    });
                }
            }

            return orders;
        }

        /// <summary>
        /// Generates a header CSV record in WSI's specified format with a new line terminator
        /// </summary>
        /// <param name="order">Order to generate the header for</param>
        /// <returns>CSV record with a new line terminator</returns>
        private static string GenerateOrderHeader(PickTicketModel order)
        {
            StringBuilder headerCsv = new();
            headerCsv.Append($"PTH,I,{order.PickTicketNumber},{order.OrderNumber},C,");
            headerCsv.Append($"{order.OrderDate.ToString("MM/dd/yyyy")},");
            headerCsv.Append(new string(',', 3));
            headerCsv.Append("75,");
            headerCsv.Append(new string(',', 2));

            headerCsv.Append($"\"{order.Customer.Name}\",");
            headerCsv.Append($"\"{order.Customer.Street}\",");
            headerCsv.Append($"\"{order.Customer.City}\",");
            headerCsv.Append($"{order.Customer.State},");
            headerCsv.Append($"{order.Customer.Country},");
            headerCsv.Append($"{order.Customer.Zip},,");

            headerCsv.Append($"\"{order.Recipient.Name}\",");
            headerCsv.Append($"\"{order.Recipient.Street}\",");
            headerCsv.Append($"\"{order.Recipient.City}\",");
            headerCsv.Append($"{order.Recipient.State},");
            headerCsv.Append($"{order.Recipient.Country},");
            headerCsv.Append($"{order.Recipient.Zip}{new string(',', 8)}");

            headerCsv.Append(order.ShippingMethod + new string(',', 3));
            headerCsv.Append("PGD,,HN,PGD,PP");
            headerCsv.Append(new string(',', 6));
            headerCsv.Append('Y' + new string(',', 4));
            headerCsv.Append("PT" + new string(',', 12));
            headerCsv.AppendLine();

            return headerCsv.ToString();
        }

        /// <summary>
        /// Generates a series of CSV detail records in WSI's specified format with a new line terminator
        /// </summary>
        /// <param name="order">Order to generate detail records for</param>
        /// <returns>CSV records separated by new line terminators</returns>
        private async Task<string> GenerateOrderDetail(string pickTicketNumber, PickTicketDetailModel lineItem)
        {
            StringBuilder detailCsv = new();

            detailCsv.Append("PTD,I,");
            detailCsv.Append($"{pickTicketNumber},");
            detailCsv.Append($"{lineItem.LineNumber},A,");
            detailCsv.Append($"{lineItem.Sku}{new string(',', 5)}");
            detailCsv.Append($"{lineItem.Units},{lineItem.Units}{new string(',', 3)}");

            HttpResponseMessage response = await _magento.GetAsync($"/api/products/{lineItem.Sku}");
            response.EnsureSuccessStatusCode();
            HttpContent content = response.Content;

            MagentoProduct product = JsonSerializer.Deserialize<MagentoProduct>(await content.ReadAsStringAsync(), _jsonOptions);

            detailCsv.Append($"{product.Price}{new string(',', 3)}");
            detailCsv.Append($"HN,PGD{new string(',', 8)}");
            detailCsv.AppendLine();


            return detailCsv.ToString();
        }
    }
}
