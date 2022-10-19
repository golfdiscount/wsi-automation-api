using Microsoft.Data.SqlClient;
using WsiApi.Models;

namespace WsiApi.Data
{
    public static class Headers
    {
        private static readonly string Select = @"SELECT * FROM [header] WHERE [header].[order_number] = @number;";
        private static readonly string Insert = @"INSERT INTO [header] (pick_ticket_number, order_number, store, customer, recipient, shipping_method, order_date, channel)
            VALUES (@pick_ticket_number, @order_number, @store, @customer, @recipient, @shipping_method, @order_date, @channel);";

        public static HeaderModel GetHeader(string orderNumber, SqlConnection conn)
        {
            if (conn.State != System.Data.ConnectionState.Open)
            {
                conn.Open();
            }

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
                PickticketNumber = reader.GetString(pickticketNumberIdx),
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
    
        public static void InsertHeader(HeaderModel header, SqlConnection conn, SqlTransaction transaction)
        {
            using SqlCommand cmd = new(Insert, conn);
            cmd.Transaction = transaction;

            cmd.Parameters.Add("@pick_ticket_number", System.Data.SqlDbType.VarChar).Value = header.PickticketNumber;
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
