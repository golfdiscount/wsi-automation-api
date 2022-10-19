using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using WsiApi.Models.ShippingMethod;

namespace WsiApi.Data
{
    public static class ShippingMethods
    {
        private static readonly string Select = @"SELECT * FROM [shipping_method]";

        public static List<GetShippingMethodModel> GetShippingMethods(string cs)
        {
            using SqlConnection conn = new(cs);
            List<GetShippingMethodModel> shippingMethods = new();
            using SqlCommand cmd = new(Select, conn);
            conn.Open();

            using SqlDataReader reader = cmd.ExecuteReader();

            int codeIdx = reader.GetOrdinal("code");
            int descriptionIdx = reader.GetOrdinal("description");
            int createdIdx = reader.GetOrdinal("created_at");
            int updatedIdx = reader.GetOrdinal("updated_at");

            while (reader.Read())
            {
                GetShippingMethodModel method = new()
                {
                    Code = reader.GetString(codeIdx),
                    Description = reader.GetString(descriptionIdx),
                    Created_at = reader.GetDateTime(createdIdx),
                    Updated_at = reader.GetDateTime(updatedIdx)
                };

                shippingMethods.Add(method);
            }

            return shippingMethods;
        }

        public static GetShippingMethodModel GetShippingMethods(string code, string cs)
        {
            using SqlConnection conn = new(cs);
            using SqlCommand cmd = new(Select + " WHERE [shipping_method].[code] = @code", conn);
            cmd.Parameters.Add("@code", System.Data.SqlDbType.VarChar).Value = code;
            conn.Open();

            using SqlDataReader reader = cmd.ExecuteReader();

            int codeIdx = reader.GetOrdinal("code");
            int descriptionIdx = reader.GetOrdinal("description");
            int createdIdx = reader.GetOrdinal("created_at");
            int updatedIdx = reader.GetOrdinal("updated_at");

            if (!reader.HasRows)
            {
                return null;
            }

            reader.Read();
            
            return new GetShippingMethodModel()
            {
                Code = reader.GetString(codeIdx),
                Description = reader.GetString(descriptionIdx),
                Created_at = reader.GetDateTime(createdIdx),
                Updated_at = reader.GetDateTime(updatedIdx)
            };
        }

        public static ShippingMethodModel GetBasicShippingMethod(string code, string cs)
        {
            using SqlConnection conn = new(cs);
            using SqlCommand cmd = new(Select + " WHERE [shipping_method].[code] = @code", conn);
            cmd.Parameters.Add("@code", System.Data.SqlDbType.VarChar).Value = code;
            conn.Open();

            using SqlDataReader reader = cmd.ExecuteReader();

            int codeIdx = reader.GetOrdinal("code");
            int descriptionIdx = reader.GetOrdinal("description");

            if (!reader.HasRows)
            {
                return null;
            }

            reader.Read();

            return new ShippingMethodModel()
            {
                Code = reader.GetString(codeIdx),
                Description = reader.GetString(descriptionIdx)
            };
        }
    }
}
