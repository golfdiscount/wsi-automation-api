﻿using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using wsi_triggers.Models.Address;

namespace wsi_triggers.Data
{
    public static class Addresses
    {
        private static readonly string Select = @"SELECT * FROM [address] WHERE [address].[id] = @id";
        private static readonly string Insert = @"INSERT INTO [address] (name, street, city, state, country, zip)
            VALUES (@name, @street, @city, @state, @country, @zip);
            SELECT CONVERT(INT, SCOPE_IDENTITY());";

        public static AddressModel GetAddress(int id, SqlConnection conn)
        {
            if (conn.State == System.Data.ConnectionState.Closed)
            {
                conn.Open();
            }

            using SqlCommand cmd = new(Select, conn);
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

            reader.Read();
            
            AddressModel address = new()
            {
                Name = reader.GetString(nameIdx),
                Street = reader.GetString(streetIdx),
                City = reader.GetString(cityIdx),
                State = reader.GetString(stateIdx),
                Country = reader.GetString(countryIdx),
                Zip = reader.GetString(zipIdx),
            };

            conn.Close();
            return address;
        }

        public static int InsertAddress(AddressModel address, SqlConnection conn, SqlTransaction transaction)
        {
            using SqlCommand cmd = new(Insert, conn);
            cmd.Transaction = transaction;

            cmd.Parameters.Add("@name", System.Data.SqlDbType.VarChar).Value = address.Name;
            cmd.Parameters.Add("@street", System.Data.SqlDbType.VarChar).Value = address.Street;
            cmd.Parameters.Add("@city", System.Data.SqlDbType.VarChar).Value = address.City;
            cmd.Parameters.Add("@state", System.Data.SqlDbType.VarChar).Value = address.State;
            cmd.Parameters.Add("@country", System.Data.SqlDbType.VarChar).Value = address.Country;
            cmd.Parameters.Add("@zip", System.Data.SqlDbType.VarChar).Value = address.Zip;

            object result = cmd.ExecuteScalar();
            int insertId = (Int32)result;
            return insertId;
        }
    }
}
