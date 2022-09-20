using Azure.Storage.Queues;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using wsi_triggers.Data;
using wsi_triggers.Models.Order;
using wsi_triggers.Models;
using wsi_triggers.Models.Detail;
using System.Web.Http;

namespace wsi_triggers.HTTP_Triggers.POST
{    
    public class PostOrder
    {
        private readonly string cs;
        private readonly JsonSerializerOptions jsonOptions;
        private readonly ILogger logger;

        public PostOrder(SqlConnectionStringBuilder builder, JsonSerializerOptions jsonOptions)
        {
            cs = builder.ConnectionString;
            this.jsonOptions = jsonOptions;
        }

        [FunctionName("PostOrder")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")] HttpRequest req, ILogger logger)
        {
            StreamReader reader = new(req.Body);
            string requestContents = reader.ReadToEnd();

            if (req.ContentType == "application/json")
            {
                
                try
                {
                    PostOrderModel order = JsonSerializer.Deserialize<PostOrderModel>(requestContents, jsonOptions);
                    InsertOrder(order);
                    return new CreatedResult("", order);
                } catch (ValidationException e)
                {
                    logger.LogWarning(e.ValidationResult.ErrorMessage);
                    return new BadRequestErrorMessageResult(e.ValidationResult.ErrorMessage);
                } catch (Exception e)
                {
                    logger.LogCritical(e.Message);
                    throw;
                }
            } else if (req.ContentType == "text/csv")
            {
                throw new NotImplementedException();
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

                QueueOrderCsvCreation(order.OrderNumber);
                cmd.CommandText = "COMMIT;";
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
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
    }
}
