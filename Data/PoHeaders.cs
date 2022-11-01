using Microsoft.Data.SqlClient;
using WsiApi.Models;

namespace WsiApi.Data
{
    public static class PoHeaders
    {
        private static readonly string Select = @"SELECT * FROM [po_header] WHERE [po_header].[po_number] = @po_number;";
        private static readonly string Insert = @"INSERT INTO [po_header] (po_number)
            VALUES (@po_number);";

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
            int createdAtIdx = reader.GetOrdinal("created_at");
            int updatedAtIdx = reader.GetOrdinal("updated_at");

            reader.Read();

            return new()
            {
                PoNumber = reader.GetString(poNumberIdx),
                Action = reader.GetString(actionIdx)[0],
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

            cmd.ExecuteScalar();
        }
    }
}
