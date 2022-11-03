using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using WsiApi.Models;

namespace WsiApi.Data
{
    public static class Stores
    {
        private static readonly string Select = @"SELECT [address].[name],
                [address].[street],
                [address].[city],
	            [address].[state],
	            [address].[country],
	            [address].[zip],
	            [store].[store_number] AS [storeNumber]
            FROM [store]
            JOIN [address] ON [address].[id] = [store].[address]";

        public static List<StoreModel> GetStore(string cs)
        {
            using SqlConnection conn = new(cs);
            List<StoreModel> stores = new();

            using SqlCommand cmd = new(Select, conn);
            conn.Open();

            using SqlDataReader reader = cmd.ExecuteReader();

            int nameIdx = reader.GetOrdinal("name");
            int streetIdx = reader.GetOrdinal("street");
            int cityIdx = reader.GetOrdinal("city");
            int stateIdx = reader.GetOrdinal("state");
            int countryIdx = reader.GetOrdinal("country");
            int zipIdx = reader.GetOrdinal("zip");
            int storeNumberIdx = reader.GetOrdinal("storeNumber");

            while(reader.Read())
            {
                StoreModel store = new()
                {
                    Name = reader.GetString(nameIdx),
                    Street = reader.GetString(streetIdx),
                    City = reader.GetString(cityIdx),
                    State = reader.GetString(stateIdx),
                    Country = reader.GetString(countryIdx),
                    Zip = reader.GetString(zipIdx),
                    StoreNumber = reader.GetInt32(storeNumberIdx)
                };

                stores.Add(store);
            }

            return stores;
        }

        public static List<StoreModel> GetStore(int id, string cs)
        {
            using SqlConnection conn = new(cs);
            List<StoreModel> stores = new();
            using SqlCommand cmd = new(Select + " WHERE [store].[id] = @id", conn);
            cmd.Parameters.Add("@id", System.Data.SqlDbType.Int).Value = id;
            conn.Open();

            using SqlDataReader reader = cmd.ExecuteReader();

            int nameIdx = reader.GetOrdinal("name");
            int streetIdx = reader.GetOrdinal("street");
            int cityIdx = reader.GetOrdinal("city");
            int stateIdx = reader.GetOrdinal("state");
            int countryIdx = reader.GetOrdinal("country");
            int zipIdx = reader.GetOrdinal("zip");
            int storeNumberIdx = reader.GetOrdinal("storeNumber");

            while (reader.Read())
            {
                StoreModel store = new()
                {
                    Name = reader.GetString(nameIdx),
                    Street = reader.GetString(streetIdx),
                    City = reader.GetString(cityIdx),
                    State = reader.GetString(stateIdx),
                    Country = reader.GetString(countryIdx),
                    Zip = reader.GetString(zipIdx),
                    StoreNumber = reader.GetInt32(storeNumberIdx)
                };

                stores.Add(store);
            }

            return stores;
        }
    }
}
