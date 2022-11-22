using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web.Http;
using WsiApi.Data;
using WsiApi.Models;
using System.Text;
using WsiApi.Services;

namespace WsiApi.HTTP_Triggers
{
    public class Orders
    {
        private readonly string _cs;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SftpService _wsiSftp;
        private readonly HttpClient _magento;

        public Orders(SqlConnectionStringBuilder builder,
            JsonSerializerOptions jsonOptions,
            SftpService wsiSftp,
            IHttpClientFactory httpClientFactory)
        {
            _cs = builder.ConnectionString;
            _jsonOptions = jsonOptions;
            _wsiSftp = wsiSftp;
            _magento = httpClientFactory.CreateClient("magento");
        }

        [FunctionName("Orders")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "orders/{orderNumber?}")] HttpRequest req,
            string? orderNumber,
            ILogger log)
        {
            if (req.Method == "POST")
            {
                return await Post(req, log);
            }
            if (orderNumber == null)
            {
                return Get();
            }

            return Get(orderNumber, log);
        }

        /// <summary>
        /// Processes a GET request and returns the most recently inserted orders
        /// </summary>
        /// <returns>HTTP Status result</returns>
        private IActionResult Get()
        {
            List<HeaderModel> headers = PtHeaders.GetHeader(_cs);
            List<OrderModel> orders = new();

            headers.ForEach(header =>
            {
                OrderModel order = new()
                {
                    PickTicketNumber = header.PickticketNumber,
                    OrderNumber = header.OrderNumber,
                    Action = header.Action,
                    Store = Data.Stores.GetStore(header.Store, _cs)[0].StoreNumber,
                    Customer = Addresses.GetAddress(header.Customer, _cs),
                    Recipient = Addresses.GetAddress(header.Recipient, _cs),
                    ShippingMethod = Data.ShippingMethods.GetShippingMethods(header.ShippingMethod, _cs).Code,
                    LineItems = PtDetails.GetDetails(header.PickticketNumber, _cs),
                    OrderDate = header.OrderDate,
                    Channel = header.Channel,
                    CreatedAt = header.CreatedAt,
                    UpdatedAt = header.UpdatedAt
                };

                orders.Add(order);
            });


            return new OkObjectResult(orders);
        }

        /// <summary>
        /// Processes a GET request for a singular order
        /// </summary>
        /// <param name="orderNumber">Order number to search for in the database</param>
        /// <param name="log">Logging object</param>
        /// <returns></returns>
        private IActionResult Get(string orderNumber, ILogger log)
        {
            log.LogInformation($"Searching database for order {orderNumber}");

            HeaderModel header = PtHeaders.GetHeader(orderNumber, _cs);

            if (header == null)
            {
                return new NotFoundResult();
            }

            OrderModel order = new()
            {
                PickTicketNumber = header.PickticketNumber,
                OrderNumber = header.OrderNumber,
                Action = header.Action,
                Store = Data.Stores.GetStore(header.Store, _cs)[0].StoreNumber,
                Customer = Addresses.GetAddress(header.Customer, _cs),
                Recipient = Addresses.GetAddress(header.Recipient, _cs),
                ShippingMethod = Data.ShippingMethods.GetShippingMethods(header.ShippingMethod, _cs).Code,
                LineItems = PtDetails.GetDetails(header.PickticketNumber, _cs),
                OrderDate = header.OrderDate,
                Channel = header.Channel,
                CreatedAt = header.CreatedAt,
                UpdatedAt = header.UpdatedAt
            };

            return new OkObjectResult(order);
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

            try
            {
                string csv;

                if (req.ContentType == "application/json")
                {
                    OrderModel order = JsonSerializer.Deserialize<OrderModel>(requestContents, _jsonOptions);
                    order.Channel = 2; // TODO: Remove channel hardcoding
                    PostJson(order, log);

                    csv = GenerateOrderHeader(order);
                    csv += GenerateOrderDetail(order);
                }
                else if (req.ContentType == "text/csv")
                {
                    PostCsv(requestContents, log);
                    csv = requestContents;
                }
                else
                {
                    log.LogWarning("Incoming request did not have Content-Type header of application/json or text/csv");
                    return new BadRequestErrorMessageResult("Request header Content-Type is not either application/json or text/csv");
                }

                Stream fileContents = new MemoryStream();
                StreamWriter writer = new(fileContents);
                writer.Flush();

                string fileName = $"PT_WSI_{DateTime.Now:MM_dd_yyyy_HH_mm_ss}.csv";

                log.LogInformation($"Queuing {fileName} for SFTP");
                _wsiSftp.Queue(fileName, fileContents);
                int fileUploadCount = _wsiSftp.UploadQueue();

                return new StatusCodeResult(201);
            }
            catch (ValidationException e)
            {
                log.LogWarning(e.ValidationResult.ErrorMessage);
                return new BadRequestErrorMessageResult(e.ValidationResult.ErrorMessage);
            }
            catch (FormatException e)
            {
                log.LogWarning(e.Message);
                return new BadRequestErrorMessageResult("Formatting error. Ensure that dates are in format YYYY-MM-DD.");
            }
            catch (SqlException e)
            {
                if (e.Number == 2627) // e.Number refers to the SQL error code
                {
                    log.LogInformation("Order already exists");
                    return new BadRequestErrorMessageResult("Order already exists");
                }

                log.LogError(e.Message);
                return new InternalServerErrorResult();
            }
        }

        private void PostJson(OrderModel order, ILogger log)
        {
            log.LogInformation($"Inserting {order.OrderNumber} into the database");
            InsertOrder(order);
        }

        private void PostCsv(string csv, ILogger log)
        {
            Dictionary<string, OrderModel> orders = ParseCsv(csv);

            foreach (var order in orders)
            {
                log.LogInformation($"Inserting {order.Value.OrderNumber} into the database");
                InsertOrder(order.Value);
            }
        }

        /// <summary>
        /// Inserts a singular order into the database
        /// </summary>
        /// <param name="order">Order to be inserted</param>
        private void InsertOrder(OrderModel order)
        {
            try
            {
                ValidateOrder(order);
            }
            catch (ValidationException)
            {
                throw;
            }

            try
            {
                int customerId = Addresses.InsertAddress(order.Customer, _cs);
                int recipientId;

                if (order.Customer.Equals(order.Recipient))
                {
                    recipientId = customerId;
                }
                else
                {
                    recipientId = Addresses.InsertAddress(order.Recipient, _cs);
                }


                HeaderModel header = new()
                {
                    PickticketNumber = "C" + order.OrderNumber,
                    OrderNumber = order.OrderNumber,
                    Action = 'I',
                    Store = order.Store,
                    Customer = customerId,
                    Recipient = recipientId,
                    ShippingMethod = order.ShippingMethod,
                    OrderDate = order.OrderDate,
                    Channel = order.Channel
                };

                PtHeaders.InsertHeader(header, _cs);

                int productCount = 0;
                order.LineItems.ForEach(product =>
                {
                    productCount++;
                    DetailModel detail = new()
                    {
                        PickticketNumber = header.PickticketNumber,
                        Action = 'I',
                        LineNumber = productCount,
                        Sku = product.Sku,
                        Units = product.Units,
                        UnitsToShip = product.Units
                    };
                    PtDetails.InsertDetail(detail, _cs);
                });
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Validates an order to ensure that all data annotations are
        /// met
        /// </summary>
        /// <param name="order">Order to be validated</param>
        private static void ValidateOrder(OrderModel order)
        {
            ValidationContext validationContext = new(order);
            Validator.ValidateObject(order, validationContext, true);

            validationContext = new(order.Customer);
            Validator.ValidateObject(order.Customer, validationContext, true);

            validationContext = new(order.Recipient);
            Validator.ValidateObject(order.Recipient, validationContext, true);
        }

        /// <summary>
        /// Parses a CSV containg pickticket headers and details
        /// </summary>
        /// <param name="csv">CSV to parse</param>
        /// <returns>A key value of pair of order number to an OrderModel</returns>
        private static Dictionary<string, OrderModel> ParseCsv(string csv)
        {
            Dictionary<string, OrderModel> orders = new();
            string[] records = csv.Split("\n");

            foreach (string record in records)
            {
                string[] fields = record.Split(',');
                string recordType = fields[0];
                string pickticketNum = fields[2];

                if (!orders.ContainsKey(pickticketNum))
                {
                    orders.Add(pickticketNum, new());
                    orders[pickticketNum].LineItems = new();
                    orders[pickticketNum].Channel = 1;
                }

                OrderModel order = orders[pickticketNum];

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
                        Units = int.Parse(fields[10])
                    });
                }
            }

            return orders;
        }

        private class OrderModel
        {            
            public string PickTicketNumber { get; set; }

            [Required]
            public string OrderNumber { get; set; }

            [Required]
            public char Action { get; set; }

            [Required]
            public int Store { get; set; }

            [Required]
            public AddressModel Customer { get; set; }

            [Required]
            public AddressModel Recipient { get; set; }

            [Required]
            public string ShippingMethod { get; set; }

            [Required]
            public List<DetailModel> LineItems { get; set; }

            [Required]
            public DateTime OrderDate { get; set; }

            public int Channel { get; set; }

            public DateTime CreatedAt { get; set; }

            public DateTime UpdatedAt { get; set; }
        }

        private static string GenerateOrderHeader(OrderModel order)
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
            headerCsv.Append('\n');

            return headerCsv.ToString();
        }

        private string GenerateOrderDetail(OrderModel order)
        {
            StringBuilder detailCsv = new();

            order.LineItems.ForEach(async lineItem =>
            {
                detailCsv.Append("PTD,I,");
                detailCsv.Append($"{order.PickTicketNumber},");
                detailCsv.Append($"{lineItem.LineNumber},A,");
                detailCsv.Append($"{lineItem.Sku}{new string(',', 5)}");
                detailCsv.Append($"{lineItem.Units},{lineItem.Units}{new string(',', 3)}");

                HttpResponseMessage response = await _magento.GetAsync($"/api/products/{lineItem.Sku}");
                response.EnsureSuccessStatusCode();
                HttpContent content = response.Content;

                MagentoProduct product = JsonSerializer.Deserialize<MagentoProduct>(await content.ReadAsStringAsync(), _jsonOptions);

                detailCsv.Append($"{product.Price}{new string(',', 3)}");
                detailCsv.Append($"HN,PGD{new string(',', 8)}");
                detailCsv.Append('\n');
            });

            return detailCsv.ToString();
        }
    }
}
