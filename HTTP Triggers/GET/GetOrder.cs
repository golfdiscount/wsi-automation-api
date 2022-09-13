using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using wsi_triggers.Data;
using wsi_triggers.Models;
using wsi_triggers.Models.Order;

namespace wsi_triggers.HTTP_Triggers.GET
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
            List<GetOrderModel> orders = new();

            List<HeaderModel> headers = Headers.GetHeaders(orderNumber, cs);

            headers.ForEach(header =>
            {
                GetOrderModel order = new()
                {
                    PickticketNumber = header.PickticketNumber,
                    OrderNumber = header.OrderNumber,
                    Action = header.Action,
                    Store = Stores.GetStore(header.Store, cs)[0],
                    Customer = Addresses.GetAddress(header.Customer, cs)[0],
                    Recipient = Addresses.GetAddress(header.Recipient, cs)[0],
                    ShippingMethod = ShippingMethods.GetBasicShippingMethod(header.ShippingMethod, cs),
                    LineItems = Details.GetDetails(header.PickticketNumber, cs),
                    OrderDate = header.OrderDate,
                    Channel = header.Channel,
                    CreatedAt = header.CreatedAt,
                    UpdatedAt = header.UpdatedAt
                };

                orders.Add(order);
            });

            if (orders.Count == 0)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(orders);           
        }
    }
}
