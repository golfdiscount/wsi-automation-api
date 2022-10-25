using Microsoft.Data.SqlClient;
using WsiApi.Models;

namespace WsiApi.Data
{
    public static class PoHeaders
    {
        private static readonly string Select = @"SELECT * FROM [po_header] WHERE [po_header].[po_number] = @po_number;";
        private static readonly string Insert = @"INSERT INTO [po_header] (po_number, po_date, delivery_date)
            VALUES (@po_number, @po_date, @delivery_date);";

        public static PoHeaderModel GetHeader(string po_number, string connectionString)
        {
            using SqlConnection conn = new(connectionString);
            conn.Open();

            using SqlCommand cmd = new(Select, conn);
            cmd.Parameters.AddWithValue("@po_number", po_number);

            using SqlDataReader reader = cmd.ExecuteReader();

            if (!reader.HasRows)
            {
                return null;
            }

            int poNumberIdx = reader.GetOrdinal("po_number");
            int actionIdx = reader.GetOrdinal("action");
            int poDateIdx = reader.GetOrdinal("po_date");
            int deliveryDateIdx = reader.GetOrdinal("delivery_date");
            int createdAtIdx = reader.GetOrdinal("created_at");
            int updatedAtIdx = reader.GetOrdinal("updated_at");

            reader.Read();

            return new()
            {
                PoNumber = reader.GetString(poNumberIdx),
                Action = reader.GetString(actionIdx)[0],
                PoDate = reader.GetDateTime(poDateIdx),
                DeliveryDate = reader.GetDateTime(deliveryDateIdx),
                CreatedAt = reader.GetDateTime(createdAtIdx),
                UpdatedAt = reader.GetDateTime(updatedAtIdx)
            };
        }

        public static void InsertHeader(PoHeaderModel header, string connectionString)
        {
            using SqlConnection conn = new(connectionString);
            conn.Open();

            using SqlCommand cmd = new(Insert, conn);

            cmd.Parameters.AddWithValue("@po_number", header.PoNumber);
            cmd.Parameters.AddWithValue("@po_date", header.PoDate);
            cmd.Parameters.AddWithValue("@delivery_date", header.DeliveryDate);

            cmd.ExecuteScalar();
        }
    }
}
