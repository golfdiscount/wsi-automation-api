using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using wsi_triggers.Models;

namespace wsi_triggers.Data
{
    public static class Headers
    {
        private static readonly string Select = @"SELECT * FROM [header] WHERE [header].[order_number] = @number;";

        public static List<Header> GetHeaders(string orderNumber, string cs)
        {
            using SqlConnection conn = new(cs);
            List<Header> headers = new();
            conn.Open();
            using SqlCommand cmd = new(Select, conn);
            cmd.Parameters.Add("@number", System.Data.SqlDbType.VarChar).Value = orderNumber;

            using SqlDataReader reader = cmd.ExecuteReader();

            int pickticketNumberIdx = reader.GetOrdinal("pick_ticket_number");
            int orderNumberIdx = reader.GetOrdinal("order_number");
            int actionIdx = reader.GetOrdinal("action");
            int storeIdx = reader.GetOrdinal("store");
            int customerIdx = reader.GetOrdinal("customer");
            int recipientIdx = reader.GetOrdinal("recipient");
            int shippingMethodIdx = reader.GetOrdinal("shipping_method");
            int orderDateIdx = reader.GetOrdinal("order_date");
            int channelIdx = reader.GetOrdinal("channel");
            int createdIdx = reader.GetOrdinal("created_at");
            int updatedIdx = reader.GetOrdinal("updated_at");


            while (reader.Read())
            {
                Header header = new()
                {
                    PickticketNumber = reader.GetString(pickticketNumberIdx),
                    OrderNumber = reader.GetString(orderNumberIdx),
                    Action = reader.GetString(actionIdx)[0],
                    Store = reader.GetInt32(storeIdx),
                    Customer = reader.GetInt32(customerIdx),
                    Recipient = reader.GetInt32(recipientIdx),
                    ShippingMethod = reader.GetInt32(shippingMethodIdx),
                    OrderDate = reader.GetDateTime(orderDateIdx),
                    Channel = reader.GetInt32(channelIdx),
                    CreatedAt = reader.GetDateTime(createdIdx),
                    UpdatedAt = reader.GetDateTime(updatedIdx)
                };

                headers.Add(header);
            }

            return headers;
        }
    }
}
