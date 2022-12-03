using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using WsiApi.Models.PurchaseOrder;

namespace WsiApi.Data
{
    public class PoDetails
    {
        private static readonly string Select = @"SELECT * FROM [po_detail] WHERE [po_detail].[po_number] = @po_number;";
        private static readonly string Insert = @"INSERT INTO [po_detail] (po_number, line_number, sku, units)
            VALUES (@po_number, @line_number, @sku, @units);";

        public static List<PurchaseOrderDetailModel> GetDetail(string po_number, string connectionString)
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

            List<PurchaseOrderDetailModel> details = new();

            int poNumberIdx = reader.GetOrdinal("po_number");
            int lineNumberIdx = reader.GetOrdinal("line_number");
            int actionIdx = reader.GetOrdinal("action");
            int skuIdx = reader.GetOrdinal("sku");
            int unitsIdx = reader.GetOrdinal("units");
            int createdAtIdx = reader.GetOrdinal("created_at");
            int updatedAtIdx = reader.GetOrdinal("updated_at");

            reader.Read();

            while (reader.Read())
            {
                details.Add(new()
                {
                    PoNumber = reader.GetString(poNumberIdx),
                    Action = reader.GetString(actionIdx)[0],
                    LineNumber = reader.GetInt32(lineNumberIdx),
                    Sku = reader.GetString(skuIdx),
                    Units = reader.GetInt32(unitsIdx),
                    CreatedAt = reader.GetDateTime(createdAtIdx),
                    UpdatedAt = reader.GetDateTime(updatedAtIdx)
                });
            }

            return details;
        }

        public static void InsertDetail(PurchaseOrderDetailModel detail, string connectionString)
        {
            using SqlConnection conn = new(connectionString);
            conn.Open();

            using SqlCommand cmd = new(Insert, conn);

            cmd.Parameters.AddWithValue("@po_number", detail.PoNumber);
            cmd.Parameters.AddWithValue("@line_number", detail.LineNumber);
            cmd.Parameters.AddWithValue("@sku", detail.Sku);
            cmd.Parameters.AddWithValue("@units", detail.Units);

            cmd.ExecuteScalar();
        }
    }
}
