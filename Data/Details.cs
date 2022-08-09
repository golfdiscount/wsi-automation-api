using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using wsi_triggers.Models;

namespace wsi_triggers.Data
{
    public static class Details
    {
        private static readonly string Select = @"SELECT * FROM [detail] WHERE [detail].[pick_ticket_number] = @number";
        public static List<Detail> GetDetails(string pickticketNumber, string cs)
        {
            using SqlConnection conn = new(cs);
            List<Detail> details = new();
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
                Detail detail = new()
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
    }
}
