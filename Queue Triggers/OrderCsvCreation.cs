using Microsoft.Azure.WebJobs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Text;
using wsi_triggers.Data;
using wsi_triggers.Models;
using wsi_triggers.Models.Address;

namespace wsi_triggers.Queue_Triggers
{
    public class OrderCsvCreation
    {
        private readonly string cs;
        public OrderCsvCreation(SqlConnectionStringBuilder builder)
        {
            cs = builder.ConnectionString;
        }

        [FunctionName("OrderCsvCreation")]
        public void Run([QueueTrigger("order-csv-creation", Connection = "AzureWebJobsStorage")]string orderNumber, ILogger log)
        {
            log.LogInformation($"Generating CSV for order {orderNumber}");

            using SqlConnection conn = new(cs);
            conn.Open();
            HeaderModel header = Headers.GetHeader(orderNumber, conn);

            StringBuilder headerCsv = new();
            headerCsv.Append("PTH,");
            headerCsv.Append(header.Action + ',');
            headerCsv.Append(header.PickticketNumber + ',');
            headerCsv.Append(header.OrderNumber + ',');
            headerCsv.Append("C,");
            headerCsv.Append(header.OrderDate.ToString("MM/dd/yyyy") + ',');
            headerCsv.Append(new string(',', 3));
            headerCsv.Append("75,");
            headerCsv.Append(new string(',', 2));

            AddressModel customer = Addresses.GetAddress(header.Customer, conn);

            headerCsv.Append(customer.Name + ",");
            headerCsv.Append(customer.Street + ",");
            headerCsv.Append(customer.City + ",");
            headerCsv.Append(customer.State + ",");
            headerCsv.Append(customer.Country + ",");
            headerCsv.Append(customer.Zip + ",,");

            AddressModel recipient = Addresses.GetAddress(header.Recipient, conn);

            headerCsv.Append(recipient.Name + ",");
            headerCsv.Append(recipient.Street + ",");
            headerCsv.Append(recipient.City + ",");
            headerCsv.Append(recipient.State + ",");
            headerCsv.Append(recipient.Country + ",");
            headerCsv.Append(recipient.Zip + new string(',', 8));

            headerCsv.Append(header.ShippingMethod + new string(',', 3));
            headerCsv.Append("PGD,,HN,PGD,PP");
            headerCsv.Append(new string(',', 6));
            headerCsv.Append('Y' + new string(',', 4));
            headerCsv.Append("PT" + new string(',', 12));

            log.LogInformation(headerCsv.ToString());
        }
    }
}
