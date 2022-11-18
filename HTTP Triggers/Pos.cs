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

namespace WsiApi.HTTP_Triggers
{
    public class Pos
    {
        private readonly string cs;

        public Pos(SqlConnectionStringBuilder builder)
        {
            cs = builder.ConnectionString;
        }

        [FunctionName("Pos")]
        public IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "pos/{poNumber}")] HttpRequest req,
            string poNumber,
            ILogger log)
        {
            log.LogInformation($"Searching for PO {poNumber}");

            PoHeaderModel header = PoHeaders.GetHeader(poNumber, cs);

            if (header == null)
            {
                return new NotFoundResult();
            }

            List<PoDetailModel> details = PoDetails.GetDetail(poNumber, cs);

            PoModel po = new()
            {
                PoNumber = header.PoNumber,
                Action = header.Action,
                CreatedAt = header.CreatedAt,
                UpdatedAt = header.UpdatedAt,
                LineItems = new()
            };

            foreach (PoDetailModel detail in details)
            {
                po.LineItems.Add(detail);
            }

            return new OkObjectResult(po);
        }

        private class PoModel
        {
            public string PoNumber { get; set; }

            public char Action { get; set; }

            public DateTime CreatedAt { get; set; }

            public DateTime UpdatedAt { get; set; }

            public List<PoDetailModel> LineItems { get; set; }
        }
    }
}
