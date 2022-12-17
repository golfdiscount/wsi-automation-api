using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using WsiApi.Data;
using WsiApi.Models.PurchaseOrder;

namespace WsiApi.HTTP_Triggers
{
    public class PurchaseOrders
    {
        private readonly string cs;

        public PurchaseOrders(SqlConnectionStringBuilder builder)
        {
            cs = builder.ConnectionString;
        }

        [FunctionName("PurchaseOrders")]
        public IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "pos/{poNumber?}")] HttpRequest req,
            string poNumber,
            ILogger log)
        {
            log.LogInformation($"Searching for PO {poNumber}");

            if (poNumber == null) 
            {
                List<PurchaseOrderModel> purchaseOrders = PurchaseOrder.GetPurchaseOrder(cs);
                return new OkObjectResult(purchaseOrders);
            }

            PurchaseOrderModel purchaseOrder = PurchaseOrder.GetPurchaseOrder(poNumber, cs);

            if (purchaseOrder == null)
            {
                return new NotFoundResult();
            }


            return new OkObjectResult(purchaseOrder);
        }
    }
}
