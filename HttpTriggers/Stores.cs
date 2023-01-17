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
    public class Stores
    {
        private readonly string cs;

        public Stores(SqlConnectionStringBuilder builder)
        {
            cs = builder.ConnectionString;
        }

        [FunctionName("Stores")]
        public IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "stores/{id:int?}")] HttpRequest req,
            int? id,
            ILogger log)
        {
            List<StoreModel> stores;

            if (id == null)
            {
                stores = Data.Stores.GetStore(cs);
            }
            else
            {
                stores = Data.Stores.GetStore((int)id, cs);
            }

            if (stores.Count == 0)
            {
                return new NotFoundObjectResult(stores);
            }

            return new OkObjectResult(stores);
        }
    }
}
