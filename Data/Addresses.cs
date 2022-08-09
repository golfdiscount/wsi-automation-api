using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using wsi_triggers.Models;

namespace wsi_triggers.Data
{
    public static class Addresses
    {
        private static readonly string Select = @"SELECT * FROM [address] WHERE [address].[id] = @id";

        public static List<Address> GetAddress(int id, string cs)
        {
            using SqlConnection conn = new(cs);
            List<Address> addresses = new();
            using SqlCommand cmd = new(Select, conn);
            conn.Open();
            cmd.Parameters.Add("@id", System.Data.SqlDbType.Int).Value = id;

            using SqlDataReader reader = cmd.ExecuteReader();

            int idIdx = reader.GetOrdinal("id");
            int nameIdx = reader.GetOrdinal("name");
            int streetIdx = reader.GetOrdinal("street");
            int cityIdx = reader.GetOrdinal("city");
            int stateIdx = reader.GetOrdinal("state");
            int countryIdx = reader.GetOrdinal("country");
            int zipIdx = reader.GetOrdinal("zip");
            int createdIdx = reader.GetOrdinal("created_at");
            int updatedIdx = reader.GetOrdinal("updated_at");
            
            while(reader.Read())
            {
                Address address = new()
                {
                    Id = reader.GetInt32(idIdx),
                    Name = reader.GetString(nameIdx),
                    Street = reader.GetString(streetIdx),
                    City = reader.GetString(cityIdx),
                    State = reader.GetString(stateIdx),
                    Country = reader.GetString(countryIdx),
                    Zip = reader.GetString(zipIdx),
                    Created_at = reader.GetDateTime(createdIdx),
                    Updated_at = reader.GetDateTime(updatedIdx)
                };

                addresses.Add(address);
            }

            conn.Close();
            return addresses;
        }
    }
}
