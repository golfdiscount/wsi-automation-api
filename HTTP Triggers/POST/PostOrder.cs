using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using wsi_triggers.Data;
using wsi_triggers.Models.Order;
using Microsoft.Data.SqlClient;
using wsi_triggers.Models;
using wsi_triggers.Models.Detail;

namespace wsi_triggers.HTTP_Triggers.POST
{    
    public class PostOrder
    {
        private readonly string cs;

        public PostOrder(SqlConnectionStringBuilder builder)
        {
            cs = builder.ConnectionString;
        }

        [FunctionName("PostOrder")]
        public IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")] PostOrderModel order,
            ILogger log)
        {
            ValidationResult validationResult = ValidateOrder(order);

            if (validationResult != null)
            {
                return new BadRequestObjectResult(validationResult.ErrorMessage);
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

                int productCount = 1;
                order.Products.ForEach(product =>
                {
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
                    productCount++;
                });
            } catch (Exception e)
            {
                log.LogError(e.Message);
                cmd.CommandText = "ROLLBACK;";
                cmd.ExecuteNonQuery();
                return new StatusCodeResult(500);
            }

            cmd.CommandText = "COMMIT;";
            cmd.ExecuteNonQuery();
            return new CreatedResult("", order);
        }

        private static ValidationResult ValidateOrder(PostOrderModel order)
        {
            ValidationContext validationContext = new(order);
            List<ValidationResult> results = new();
            bool valid = Validator.TryValidateObject(order, validationContext, results, true);

            if (!valid)
            {
                return results[0];
            }

            validationContext = new(order.Customer);
            valid = Validator.TryValidateObject(order.Customer, validationContext, results, true);

            if (!valid)
            {
                return results[0];
            }

            validationContext = new(order.Recipient);
            valid = Validator.TryValidateObject(order.Recipient, validationContext, results, true);

            if (!valid)
            {
                return results[0];
            }

            return null;
        }
    }
}
