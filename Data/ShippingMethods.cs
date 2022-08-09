using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using wsi_triggers.Models;

namespace wsi_triggers.Data
{
    public static class ShippingMethods
    {
        private static readonly string Select = @"SELECT * FROM [shipping_method]";

        public static List<ShippingMethod> GetShippingMethods(string cs)
        {
            using SqlConnection conn = new(cs);
            List<ShippingMethod> shippingMethods = new();
            using SqlCommand cmd = new(Select, conn);
            conn.Open();

            using SqlDataReader reader = cmd.ExecuteReader();

            int idIdx = reader.GetOrdinal("id");
            int codeIdx = reader.GetOrdinal("code");
            int descriptionIdx = reader.GetOrdinal("description");
            int createdIdx = reader.GetOrdinal("created_at");
            int updatedIdx = reader.GetOrdinal("updated_at");

            while (reader.Read())
            {
                ShippingMethod method = new()
                {
                    Id = reader.GetInt32(idIdx),
                    Code = reader.GetString(codeIdx),
                    Description = reader.GetString(descriptionIdx),
                    Created_at = reader.GetDateTime(createdIdx),
                    Updated_at = reader.GetDateTime(updatedIdx)
                };

                shippingMethods.Add(method);
            }

            return shippingMethods;
        }

        public static List<ShippingMethod> GetShippingMethods(int id, string cs)
        {
            using SqlConnection conn = new(cs);
            List<ShippingMethod> shippingMethods = new();
            using SqlCommand cmd = new(Select + " WHERE [shipping_method].[id] = @id", conn);
            cmd.Parameters.Add("@id", System.Data.SqlDbType.Int).Value = id;
            conn.Open();

            using SqlDataReader reader = cmd.ExecuteReader();

            int idIdx = reader.GetOrdinal("id");
            int codeIdx = reader.GetOrdinal("code");
            int descriptionIdx = reader.GetOrdinal("description");
            int createdIdx = reader.GetOrdinal("created_at");
            int updatedIdx = reader.GetOrdinal("updated_at");

            while (reader.Read())
            {
                ShippingMethod method = new()
                {
                    Id = reader.GetInt32(idIdx),
                    Code = reader.GetString(codeIdx),
                    Description = reader.GetString(descriptionIdx),
                    Created_at = reader.GetDateTime(createdIdx),
                    Updated_at = reader.GetDateTime(updatedIdx)
                };

                shippingMethods.Add(method);
            }

            return shippingMethods;
        }
    }
}
