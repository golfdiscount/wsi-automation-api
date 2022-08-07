using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "shipping/{id:int?}")] HttpRequest req,
            int? id,
            ILogger log)
        {
            List<ShippingMethod> methods = new();
            using SqlConnection conn = new(cs);

            string cmdText = @"SELECT * FROM  [shipping_method]";

            if (id != null)
            {
                cmdText += " WHERE [shipping_method].[id] = @id";
            }

            cmdText += ";";
            SqlCommand cmd = new(cmdText, conn);

            if (id != null)
            {
                cmd.Parameters.Add("@id", System.Data.SqlDbType.Int).Value = id;
            }

            conn.Open();

            using SqlDataReader reader = cmd.ExecuteReader();
            while(reader.Read())
            {
                ShippingMethod method = new()
                {
                    Id = reader.GetInt32(0),
                    Code = reader.GetString(1),
                    Description = reader.GetString(2),
                    Created_at = reader.GetDateTime(3),
                    Updated_at = reader.GetDateTime(4)
                };

                methods.Add(method);
            }

            if (methods.Count == 0)
            {
                return new NotFoundObjectResult(methods);
            }

            return new OkObjectResult(methods);
        }
    }
}
