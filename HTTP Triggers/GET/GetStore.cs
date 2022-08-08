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
    public class GetStore
    {
        private readonly string cs;

        public GetStore(SqlConnectionStringBuilder builder)
        {
            cs = builder.ConnectionString;
        }

        [FunctionName("GetStore")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "stores/{id:int?}")] HttpRequest req,
            int? id,
            ILogger log)
        {
            List<Store> stores = new();
            using SqlConnection conn = new(cs);

            string cmdText = @"SELECT [address].[name],
                    [address].[street],
                    [address].[city],
	                [address].[state],
	                [address].[country],
	                [address].[zip],
	                [store].[store_number] AS [storeNumber]
                FROM [store]
                JOIN [address] ON [address].[id] = [store].[address]";

            if (id != null)
            {
                cmdText += " WHERE [store].[id] = @id";
            }

            cmdText += ";";
            SqlCommand cmd = new(cmdText, conn);
            
            if (id != null)
            {
                cmd.Parameters.Add("@id", System.Data.SqlDbType.Int).Value = id;
            }

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

            if (stores.Count == 0)
            {
                return new NotFoundObjectResult(stores);
            }

            return new OkObjectResult(stores);
        }
    }
}
