using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using WsiApi.Data;
using WsiApi.Models;
using System;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Web.Http;

namespace WsiApi.HTTP_Triggers
{
    public class Orders
    {
        private readonly string cs;
        private readonly JsonSerializerOptions jsonOptions;
        private readonly BlobServiceClient blobServiceClient;
        private readonly QueueServiceClient queueServiceClient;

        public Orders(SqlConnectionStringBuilder builder,
            JsonSerializerOptions jsonOptions,
            QueueServiceClient queueServiceClient,
            BlobServiceClient blobServiceClient)
        {
            cs = builder.ConnectionString;
            this.jsonOptions = jsonOptions;
            this.queueServiceClient = queueServiceClient;
            this.blobServiceClient = blobServiceClient;
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
            List<HeaderModel> headers = PtHeaders.GetHeader(cs);
            List<OrderModel> orders = new();

            headers.ForEach(header =>
            {
                OrderModel order = new()
                {
                    PickticketNumber = header.PickticketNumber,
                    OrderNumber = header.OrderNumber,
                    Action = header.Action,
                    Store = Data.Stores.GetStore(header.Store, cs)[0].StoreNumber,
                    Customer = Addresses.GetAddress(header.Customer, cs),
                    Recipient = Addresses.GetAddress(header.Recipient, cs),
                    ShippingMethod = Data.ShippingMethods.GetShippingMethods(header.ShippingMethod, cs).Code,
                    Products = PtDetails.GetDetails(header.PickticketNumber, cs),
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

            HeaderModel header = PtHeaders.GetHeader(orderNumber, cs);

            if (header == null)
            {
                return new NotFoundResult();
            }

            OrderModel order = new()
            {
                PickticketNumber = header.PickticketNumber,
                OrderNumber = header.OrderNumber,
                Action = header.Action,
                Store = Data.Stores.GetStore(header.Store, cs)[0].StoreNumber,
                Customer = Addresses.GetAddress(header.Customer, cs),
                Recipient = Addresses.GetAddress(header.Recipient, cs),
                ShippingMethod = Data.ShippingMethods.GetShippingMethods(header.ShippingMethod, cs).Code,
                Products = PtDetails.GetDetails(header.PickticketNumber, cs),
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

            if (req.ContentType == "application/json")
            {
                try
                {
                    OrderModel order = JsonSerializer.Deserialize<OrderModel>(requestContents, jsonOptions);
                    order.Channel = 2; // TODO: Remove channel hardcoding
                    log.LogInformation($"Inserting {order.OrderNumber} into the database");
                    InsertOrder(order);

                    QueueClient orderCsvCreationQueue = queueServiceClient.GetQueueClient("order-csv-creation");
                    log.LogInformation($"Queuing {order.OrderNumber} for CSV creation");
                    await orderCsvCreationQueue.SendMessageAsync(order.OrderNumber);

                    log.LogInformation("Commiting transaction");
                    return new CreatedResult("", order);
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
                }
                catch (Exception e)
                {
                    log.LogWarning("Rolling back transaction");
                    log.LogCritical(e.Message);
                    throw;
                }
            }
            else if (req.ContentType == "text/csv")
            {
                Dictionary<string, OrderModel> orders = new();
                string[] records = requestContents.Split("\n");

                foreach (string record in records)
                {
                    string[] fields = record.Split(',');
                    string recordType = fields[0];
                    string pickticketNum = fields[2];

                    if (!orders.ContainsKey(pickticketNum))
                    {
                        orders.Add(pickticketNum, new());
                        orders[pickticketNum].Products = new();
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
                        order.Products.Add(new()
                        {
                            Sku = fields[5],
                            Units = int.Parse(fields[10])
                        });
                    }
                }

                try
                {
                    foreach (var order in orders)
                    {
                        log.LogInformation($"Inserting {order.Value.OrderNumber} into the database");
                        InsertOrder(order.Value);
                    }

                    BinaryData csvContents = new(requestContents);
                    string fileName = $"PT_WSI_{DateTime.Now:MM_dd_yyyy_HH_mm_ss}.csv";
                    BlobContainerClient sftpContainer = blobServiceClient.GetBlobContainerClient("sftp");

                    log.LogInformation($"Uploading blob {fileName} to sftp container");
                    await sftpContainer.UploadBlobAsync(fileName, csvContents);

                    log.LogInformation("Commiting transaction");
                }
                catch (ValidationException e)
                {
                    log.LogWarning(e.ValidationResult.ErrorMessage);
                    return new BadRequestErrorMessageResult(e.ValidationResult.ErrorMessage);
                }
                catch (SqlException e)
                {
                    if (e.Number == 2627) // e.Number refers to the SQL error code
                    {
                        log.LogInformation("Order already exists");
                        return new BadRequestErrorMessageResult("Order already exists");
                    }
                }
                catch (Exception e)
                {
                    log.LogCritical(e.Message);
                    throw;
                }

                return new StatusCodeResult(201);
            }

            log.LogWarning("Incoming request did not have Content-Type header of application/json or text/csv");
            return new BadRequestErrorMessageResult("Request header Content-Type is not either application/json or text/csv");
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
                int customerId = Addresses.InsertAddress(order.Customer, cs);
                int recipientId;

                if (order.Customer.Equals(order.Recipient))
                {
                    recipientId = customerId;
                }
                else
                {
                    recipientId = Addresses.InsertAddress(order.Recipient, cs);
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

                PtHeaders.InsertHeader(header, cs);

                int productCount = 0;
                order.Products.ForEach(product =>
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
                    PtDetails.InsertDetail(detail, cs);
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

        private class OrderModel
        {
            public string PickticketNumber { get; set; }

            public string OrderNumber { get; set; }

            public char Action { get; set; }

            public int Store { get; set; }

            public AddressModel Customer { get; set; }

            public AddressModel Recipient { get; set; }

            public string ShippingMethod { get; set; }

            public List<DetailModel> Products { get; set; }

            public DateTime OrderDate { get; set; }

            public int Channel { get; set; }

            public DateTime CreatedAt { get; set; }

            public DateTime UpdatedAt { get; set; }
        }
    }
}
