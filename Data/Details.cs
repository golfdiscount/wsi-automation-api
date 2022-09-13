using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using wsi_triggers.Models.Detail;

namespace wsi_triggers.Data
{
    public static class Details
    {
        private static readonly string Select = @"SELECT * FROM [detail] WHERE [detail].[pick_ticket_number] = @number";
        private static readonly string Insert = @"INSERT INTO [detail] (pick_ticket_number, line_number, action, sku, units, units_to_ship)
            VALUES (@pick_ticket_number, @line_number, @action, @sku, @quantity, @units_to_ship);";
        public static List<GetDetailModel> GetDetails(string pickticketNumber, string cs)
        {
            using SqlConnection conn = new(cs);
            List<GetDetailModel> details = new();
            conn.Open();
            using SqlCommand cmd = new(Select, conn);
            cmd.Parameters.Add("@number", System.Data.SqlDbType.VarChar).Value = pickticketNumber;

            using SqlDataReader reader = cmd.ExecuteReader();

            int pickticketIdx = reader.GetOrdinal("pick_ticket_number");
            int lineNumberIdx = reader.GetOrdinal("line_number");
            int actionIdx = reader.GetOrdinal("action");
            int skuIdx = reader.GetOrdinal("sku");
            int unitsIdx = reader.GetOrdinal("units");
            int unitsToShipIdx = reader.GetOrdinal("units_to_ship");
            int createdIdx = reader.GetOrdinal("created_at");
            int updatedIdx = reader.GetOrdinal("updated_at");

            while (reader.Read())
            {
                GetDetailModel detail = new()
                {
                    PickticketNumber = reader.GetString(pickticketIdx),
                    LineNumber = reader.GetInt32(lineNumberIdx),
                    Action = reader.GetString(actionIdx)[0],
                    Sku = reader.GetString(skuIdx),
                    Units = reader.GetInt32(unitsIdx),
                    UnitsToShip = reader.GetInt32(unitsToShipIdx),
                    Created_at = reader.GetDateTime(createdIdx),
                    Updated_at = reader.GetDateTime(updatedIdx)
                };

                details.Add(detail);
            }

            return details;
        }

        public static void InsertDetail(DetailModel detail, SqlConnection conn)
        {
            using SqlCommand cmd = new(Insert, conn);

            cmd.Parameters.AddWithValue("@pick_ticket_number", detail.PickticketNumber);
            cmd.Parameters.AddWithValue("@line_number", detail.LineNumber);
            cmd.Parameters.AddWithValue("@sku", detail.Sku);
            cmd.Parameters.AddWithValue("@action", detail.Action);
            cmd.Parameters.AddWithValue("@quantity", detail.Units);
            cmd.Parameters.AddWithValue("@units_to_ship", detail.UnitsToShip);

            cmd.ExecuteScalar();
        }
    }
}
