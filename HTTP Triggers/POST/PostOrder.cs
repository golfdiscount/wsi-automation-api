using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using wsi_triggers.Data;
using wsi_triggers.Models.Order;
using Microsoft.Data.SqlClient;
using wsi_triggers.Models;

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
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")] PostOrderModel order,
            ILogger log)
        {
            ValidationResult validationResult = ValidateOrder(order);

            if (validationResult != null)
            {
                return new BadRequestObjectResult(validationResult.ErrorMessage);
            }

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
                Channel = 2
            };

            Headers.InsertHeader(header, cs);

            return new CreatedResult("", order);
        }

        private ValidationResult ValidateOrder(PostOrderModel order)
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
