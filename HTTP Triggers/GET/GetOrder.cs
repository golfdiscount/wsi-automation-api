using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using wsi_triggers.Models;

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
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orders/{orderNumber:int?}")] HttpRequest req,
            int? orderNumber,
            ILogger log)
        {
            List<Order> orders = new List<Order>();
            using SqlConnection conn = new(cs);

            string cmdText = @"SELECT * FROM [header]";

            if (orderNumber!= null)
            {
                cmdText += " WHERE [header].[order_number] = @order_number";
            }

            SqlCommand cmd = new(cmdText, conn);

            if (orderNumber != null)
            {
                cmd.Parameters.Add("@order_number", System.Data.SqlDbType.VarChar).Value = orderNumber;
            }

            return new OkObjectResult("hello");
        }
    }
}
