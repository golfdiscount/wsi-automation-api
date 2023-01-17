using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using Pgd.Wsi.Models;

namespace Pgd.Wsi.HttpTriggers
{
    public class ShippingMethods
    {
        private readonly string cs;
        public ShippingMethods(SqlConnectionStringBuilder builder)
        {
            cs = builder.ConnectionString;
        }

        [FunctionName("ShippingMethods")]
        public IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "shipping/{code:alpha?}")] HttpRequest req,
            string? code,
            ILogger log)
        {
            if (code != null)
            {
                ShippingMethodModel method = Data.ShippingMethods.GetShippingMethods(code, cs);

                if (method == null)
                {
                    return new NotFoundResult();
                }

                return new OkObjectResult(method);
            }

            List<ShippingMethodModel> methods = Data.ShippingMethods.GetShippingMethods(cs);
            return new OkObjectResult(methods);
        }
    }
}
