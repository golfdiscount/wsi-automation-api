using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using WsiApi.Data;
using WsiApi.Models;
using WsiApi.Models.Order;

namespace WsiApi.HTTP_Triggers.GET
{
    public class GetOrder
    {
        private readonly string cs;
        public GetOrder(SqlConnectionStringBuilder builder)
        {
            cs = builder.ConnectionString;
        }

        [FunctionName("GetOrder")]
        public IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orders/{orderNumber}")] HttpRequest req,
            string orderNumber,
            ILogger log)
        {
            log.LogInformation($"Searching database for order {orderNumber}");
            using SqlConnection conn = new(cs);

            HeaderModel header = Headers.GetHeader(orderNumber, conn);

            if (header == null)
            {
                return new NotFoundResult();
            }

            GetOrderModel order = new()
            {
                PickticketNumber = header.PickticketNumber,
                OrderNumber = header.OrderNumber,
                Action = header.Action,
                Store = Stores.GetStore(header.Store, cs)[0],
                Customer = Addresses.GetAddress(header.Customer, conn),
                Recipient = Addresses.GetAddress(header.Recipient, conn),
                ShippingMethod = ShippingMethods.GetBasicShippingMethod(header.ShippingMethod, cs),
                LineItems = Details.GetDetails(header.PickticketNumber, conn),
                OrderDate = header.OrderDate,
                Channel = header.Channel,
                CreatedAt = header.CreatedAt,
                UpdatedAt = header.UpdatedAt
            };

            return new OkObjectResult(order);           
        }
    }
}
