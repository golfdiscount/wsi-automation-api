using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using wsi_triggers.Models;

namespace wsi_triggers
{
    public class Stores
    {
        private readonly string cs;

        public Stores(SqlConnectionStringBuilder builder)
        {
            cs = builder.ConnectionString;
        }

        [FunctionName("Stores")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "stores")] HttpRequest req,
            ILogger log)
        {
            List<Store> stores = new();
            using SqlConnection conn = new(cs);
            SqlCommand cmd = new(@"SELECT [address].[name],
                    [address].[address],
                    [address].[city],
	                [address].[state],
	                [address].[country],
	                [address].[zip],
	                [store].[store_number] AS [storeNumber]
                FROM [store]
                JOIN [address] ON[address].[id] = [store].[address];", conn);
            conn.Open();

            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                Store store = new()
                {
                    name = reader.GetString(0),
                    address = reader.GetString(1),
                    city = reader.GetString(2),
                    state = reader.GetString(3),
                    country = reader.GetString(4),
                    zip = reader.GetString(5),
                    storeNumber = reader.GetInt32(6)
                };

                stores.Add(store);
            }

            return new OkObjectResult(stores);
        }
    }
}
