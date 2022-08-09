using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using wsi_triggers.Data;
using wsi_triggers.Models;

namespace wsi_triggers.HTTP_Triggers
{
    public class GetShippingMethod
    {
        private readonly string cs;
        public GetShippingMethod(SqlConnectionStringBuilder builder)
        {
            cs = builder.ConnectionString;
        }

        [FunctionName("GetShippingMethod")]
        public IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "shipping/{id:int?}")] HttpRequest req,
            int? id,
            ILogger log)
        {
            List<ShippingMethod> methods;

            if (id == null)
            {
                methods = ShippingMethods.GetShippingMethods(cs);
            } else
            {
                methods = ShippingMethods.GetShippingMethods((int)id, cs);
            }

            if (methods.Count == 0)
            {
                return new NotFoundObjectResult(methods);
            }

            return new OkObjectResult(methods);
        }
    }
}
