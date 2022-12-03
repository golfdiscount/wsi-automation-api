using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Threading.Tasks;
using WsiApi.Models;

namespace WsiApi.Data
{
    public static class PtHeaders
    {
        private static readonly string Select = @"SELECT * FROM [pt_header] WHERE [pt_header].[order_number] = @number;";
        private static readonly string Insert = @"INSERT INTO [pt_header] (pick_ticket_number, order_number, store, customer, recipient, shipping_method, order_date, channel)
            VALUES (@pick_ticket_number, @order_number, @store, @customer, @recipient, @shipping_method, @order_date, @channel);";

        public static List<HeaderModel> GetHeader(string connectionString)
        {
            List<HeaderModel> headers = new();
            using SqlConnection conn = new(connectionString);
            conn.Open();

            string cmdText = @"SELECT TOP 30 *
                FROM [dbo].[pt_header]
                ORDER BY [dbo].[pt_header].[created_at] DESC,
	                [dbo].[pt_header].[order_date] DESC;";
            using SqlCommand cmd = new(cmdText, conn);

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
                HeaderModel header = new()
                {
                    PickTicketNumber = reader.GetString(pickticketNumberIdx),
                    OrderNumber = reader.GetString(orderNumberIdx),
                    Action = reader.GetString(actionIdx)[0], // Microsoft.Data.SqlClient.SqlDataReader.GetChar() is not supported
                    Store = reader.GetInt32(storeIdx),
                    Customer = reader.GetInt32(customerIdx),
                    Recipient = reader.GetInt32(recipientIdx),
                    ShippingMethod = reader.GetString(shippingMethodIdx),
                    OrderDate = reader.GetDateTime(orderDateIdx),
                    Channel = reader.GetInt32(channelIdx),
                    CreatedAt = reader.GetDateTime(createdIdx),
                    UpdatedAt = reader.GetDateTime(updatedIdx)
                };

                headers.Add(header);
            }

            return headers;
        }

        public static HeaderModel GetHeader(string orderNumber, string connectionString)
        {
            using SqlConnection conn = new(connectionString);
            conn.Open();

            using SqlCommand cmd = new(Select, conn);
            cmd.Parameters.AddWithValue("@number", orderNumber);

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

            if (!reader.HasRows)
            {
                return null;
            }

            reader.Read();

            HeaderModel header = new()
            {
                PickTicketNumber = reader.GetString(pickticketNumberIdx),
                OrderNumber = reader.GetString(orderNumberIdx),
                Action = reader.GetString(actionIdx)[0], // Microsoft.Data.SqlClient.SqlDataReader.GetChar() is not supported
                Store = reader.GetInt32(storeIdx),
                Customer = reader.GetInt32(customerIdx),
                Recipient = reader.GetInt32(recipientIdx),
                ShippingMethod = reader.GetString(shippingMethodIdx),
                OrderDate = reader.GetDateTime(orderDateIdx),
                Channel = reader.GetInt32(channelIdx),
                CreatedAt = reader.GetDateTime(createdIdx),
                UpdatedAt = reader.GetDateTime(updatedIdx)
            };

            return header;
        }
    
        public static void InsertHeader(HeaderModel header, string connectionString)
        {
            using SqlConnection conn = new(connectionString);
            conn.Open();
            using SqlCommand cmd = new(Insert, conn);

            cmd.Parameters.Add("@pick_ticket_number", System.Data.SqlDbType.VarChar).Value = header.PickTicketNumber;
            cmd.Parameters.Add("@order_number", System.Data.SqlDbType.VarChar).Value = header.OrderNumber;
            cmd.Parameters.Add("@store", System.Data.SqlDbType.Int).Value = header.Store;
            cmd.Parameters.Add("@customer", System.Data.SqlDbType.Int).Value = header.Customer;
            cmd.Parameters.Add("@recipient", System.Data.SqlDbType.Int).Value = header.Recipient;
            cmd.Parameters.Add("@shipping_method", System.Data.SqlDbType.VarChar).Value = header.ShippingMethod;
            cmd.Parameters.Add("@order_date", System.Data.SqlDbType.Date).Value = header.OrderDate;
            cmd.Parameters.Add("@channel", System.Data.SqlDbType.Int).Value = header.Channel;

            cmd.ExecuteScalar();
        }
    }
}
