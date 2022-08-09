using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using wsi_triggers.Data;
using wsi_triggers.Models;

namespace wsi_triggers
{
    public class GetStore
    {
        private readonly string cs;

        public GetStore(SqlConnectionStringBuilder builder)
        {
            cs = builder.ConnectionString;
        }

        [FunctionName("GetStore")]
        public IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "stores/{id:int?}")] HttpRequest req,
            int? id,
            ILogger log)
        {
            List<Store> stores;

            if (id == null)
            {
                stores = Stores.GetStore(cs);
            } else
            {
                stores = Stores.GetStore((int)id, cs);
            }

            if (stores.Count == 0)
            {
                return new NotFoundObjectResult(stores);
            }

            return new OkObjectResult(stores);
        }
    }
}
