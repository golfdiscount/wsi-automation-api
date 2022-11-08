using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web.Http;
using WsiApi.Data;
using WsiApi.Models;

namespace WsiApi.HTTP_Triggers.POST
{
    public class PostOrder
    {
        private readonly string cs;
        private readonly JsonSerializerOptions jsonOptions;
        private readonly BlobServiceClient blobServiceClient;
        private readonly QueueServiceClient queueServiceClient;

        public PostOrder(SqlConnectionStringBuilder builder,
            JsonSerializerOptions jsonOptions,
            QueueServiceClient queueServiceClient,
            BlobServiceClient blobServiceClient)
        {
            cs = builder.ConnectionString;
            this.jsonOptions = jsonOptions;
            this.queueServiceClient = queueServiceClient;
            this.blobServiceClient = blobServiceClient;
        }

        [FunctionName("PostOrder")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")] HttpRequest req,
            ILogger log)
        {
            StreamReader reader = new(req.Body);
            string requestContents = reader.ReadToEnd().Trim();

            if (req.ContentType == "application/json")
            {
                try
                {
                    OrderModel order = JsonSerializer.Deserialize<OrderModel>(requestContents, jsonOptions);
                    order.Channel = 2; // TODO: Remove channel hardcoding
                    InsertOrder(order);

                    QueueClient orderCsvCreationQueue = queueServiceClient.GetQueueClient("order-csv-creation");
                    log.LogInformation($"Queuing {order.OrderNumber} for CSV creation");
                    await orderCsvCreationQueue.SendMessageAsync(order.OrderNumber);

                    log.LogInformation("Commiting transaction");
                    return new CreatedResult("", order);
                } catch (ValidationException e)
                {
                    log.LogWarning(e.ValidationResult.ErrorMessage);
                    return new BadRequestErrorMessageResult(e.ValidationResult.ErrorMessage);
                } catch (FormatException e)
                {
                    log.LogWarning(e.Message);
                    return new BadRequestErrorMessageResult("Formatting error. Ensure that dates are in format YYYY-MM-DD.");
                } catch (Exception e)
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
                    } else if (recordType == "PTD")
                    {
                        order.Products.Add(new()
                        {
                            Sku = fields[5],
                            Quantity = int.Parse(fields[10])
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
                } catch (ValidationException e)
                {
                    log.LogWarning(e.ValidationResult.ErrorMessage);
                    return new BadRequestErrorMessageResult(e.ValidationResult.ErrorMessage);
                } catch (Exception e)
                {
                    log.LogCritical(e.Message);
                    throw;
                }

                return new StatusCodeResult(201);
            }

            log.LogWarning("Incoming request did not have Content-Type header of application/json or text/csv");
            return new BadRequestErrorMessageResult("Request header Content-Type is not either application/json or text/csv");
        }

        private class OrderModel
        {
            [Required]
            public string OrderNumber { get; set; }

            [Required]
            public int Store { get; set; }

            [Required]
            public AddressModel Customer { get; set; }

            [Required]
            public AddressModel Recipient { get; set; }

            [Required]
            public string ShippingMethod { get; set; }

            [Required]
            public DateTime OrderDate { get; set; }

            [Required]
            public List<LineItemModel> Products { get; set; }

            public int Channel { get; set; }
        }

        private class LineItemModel
        {
            [Required]
            public string Sku { get; set; }

            [Required]
            public int Quantity { get; set; }
        }

        private void InsertOrder(OrderModel order)
        {
            try
            {
                ValidateOrder(order);
            } catch (ValidationException)
            {
                throw;
            }

            try
            {
                int customerId = Addresses.InsertAddress(order.Customer, cs);
                int recipientId = Addresses.InsertAddress(order.Recipient, cs);

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
                        Units = product.Quantity,
                        UnitsToShip = product.Quantity
                    };
                    PtDetails.InsertDetail(detail, cs);
                });
            }
            catch (Exception)
            {
                throw;
            }
        }

        private static void ValidateOrder(OrderModel order)
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
