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
using System.Web.Http;
using wsi_triggers.Data;
using wsi_triggers.Models.Order;
using wsi_triggers.Models;
using wsi_triggers.Models.Detail;
using Azure.Storage.Blobs;

namespace wsi_triggers.HTTP_Triggers.POST
{    
    public class PostOrder
    {
        private readonly string cs;
        private readonly JsonSerializerOptions jsonOptions;

        public PostOrder(SqlConnectionStringBuilder builder, JsonSerializerOptions jsonOptions)
        {
            cs = builder.ConnectionString;
            this.jsonOptions = jsonOptions;
        }

        [FunctionName("PostOrder")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")] HttpRequest req, 
            ILogger log)
        {
            StreamReader reader = new(req.Body);
            string requestContents = reader.ReadToEnd().Trim();

            if (req.ContentType == "application/json")
            {
                try
                {
                    PostOrderModel order = JsonSerializer.Deserialize<PostOrderModel>(requestContents, jsonOptions);
                    InsertOrder(order);
                    QueueOrderCsvCreation(order.OrderNumber);
                    return new CreatedResult("", order);
                } catch (ValidationException e)
                {
                    log.LogWarning(e.ValidationResult.ErrorMessage);
                    return new BadRequestErrorMessageResult(e.ValidationResult.ErrorMessage);
                } catch (Exception e)
                {
                    log.LogCritical(e.Message);
                    throw;
                }
            } 
            else if (req.ContentType == "text/csv")
            {
                Dictionary<string, PostOrderModel> orders = new();
                string[] records = requestContents.Split("\n");

                foreach(string record in records)
                {
                    string[] fields = record.Split(',');
                    string recordType = fields[0];
                    string pickticketNum = fields[2];

                    if (!orders.ContainsKey(pickticketNum))
                    {
                        orders.Add(pickticketNum, new());
                        orders[pickticketNum].Products = new();
                    }

                    PostOrderModel order = orders[pickticketNum];

                    if (recordType == "PTH")
                    {
                        order.OrderNumber = fields[3];
                        order.OrderDate = DateTime.ParseExact(fields[5], "MM/dd/yyyy", null);
                        order.Customer = new()
                        {
                            Name = fields[12],
                            Street = fields[13],
                            City = fields[14],
                            State = fields[15],
                            Country = fields[16],
                            Zip = fields[17]
                        };
                        order.Recipient = new()
                        {
                            Name = fields[19],
                            Street = fields[20],
                            City = fields[21],
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

                    QueueCsvSftp(requestContents);
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

            return new BadRequestErrorMessageResult("Request header Content-Type is not either application/json or text/csv");
        }

        private void InsertOrder(PostOrderModel order)
        {
            try
            {
                ValidateOrder(order);
            } catch (ValidationException)
            {
                throw;
            }

            using SqlConnection conn = new(cs);
            conn.Open();

            SqlCommand cmd = new("BEGIN TRANSACTION;", conn);
            cmd.ExecuteNonQuery();

            try
            {
                int customerId = Addresses.InsertAddress(order.Customer, conn);
                int recipientId = Addresses.InsertAddress(order.Recipient, conn);

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
                    Channel = 2
                };

                Headers.InsertHeader(header, conn);

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
                    Details.InsertDetail(detail, conn);
                });

                cmd.CommandText = "COMMIT;";
                cmd.ExecuteNonQuery();
            }
            catch (Exception)
            {
                cmd.CommandText = "ROLLBACK;";
                cmd.ExecuteNonQuery();
                throw;
            }
        }

        private static void ValidateOrder(PostOrderModel order)
        {
            ValidationContext validationContext = new(order);
            Validator.ValidateObject(order, validationContext, true);

            validationContext = new(order.Customer);
            Validator.ValidateObject(order.Customer, validationContext, true);

            validationContext = new(order.Recipient);
            Validator.ValidateObject(order.Recipient, validationContext, true);
        }
    
        private static void QueueOrderCsvCreation(string orderNumber)
        {
            byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(orderNumber);
            string queueMessage = Convert.ToBase64String(messageBytes);
            QueueClient queue = new(Environment.GetEnvironmentVariable("AzureWebJobsStorage"), "order-csv-creation");
            queue.SendMessage(queueMessage);
        }

        private static void QueueCsvSftp(string csv)
        {
            BinaryData csvContents = new(csv);
            string fileName = $"PT_WSI_{DateTime.Now:MM_dd_yyyy_HH_mm_ss}.csv";
            BlobClient client = new(Environment.GetEnvironmentVariable("AzureWebJobsStorage"), "sftp", fileName);
            client.Upload(csvContents);
        }
    }
}
