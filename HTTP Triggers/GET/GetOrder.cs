using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using WsiApi.Data;
using WsiApi.Models;

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

            HeaderModel header = Headers.GetHeader(orderNumber, cs);

            if (header == null)
            {
                return new NotFoundResult();
            }

            OrderModel order = new()
            {
                PickticketNumber = header.PickticketNumber,
                OrderNumber = header.OrderNumber,
                Action = header.Action,
                Store = Stores.GetStore(header.Store, cs)[0],
                Customer = Addresses.GetAddress(header.Customer, cs),
                Recipient = Addresses.GetAddress(header.Recipient, cs),
                ShippingMethod = ShippingMethods.GetShippingMethods(header.ShippingMethod, cs),
                LineItems = Details.GetDetails(header.PickticketNumber, cs),
                OrderDate = header.OrderDate,
                Channel = header.Channel,
                CreatedAt = header.CreatedAt,
                UpdatedAt = header.UpdatedAt
            };

            return new OkObjectResult(order);           
        }

        private class OrderModel
        {
            public string PickticketNumber { get; set; }

            public string OrderNumber { get; set; }

            public char Action { get; set; }

            public StoreModel Store { get; set; }

            public AddressModel Customer { get; set; }

            public AddressModel Recipient { get; set; }

            public ShippingMethodModel ShippingMethod { get; set; }

            public List<DetailModel> LineItems { get; set; }

            public DateTime OrderDate { get; set; }

            public int Channel { get; set; }

            public DateTime CreatedAt { get; set; }

            public DateTime UpdatedAt { get; set; }
        }
    }
}
