using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Azure;
using Pgd.Wsi.Models.PickTicket;
using Pgd.Wsi.Models.ShippingConfirmation;
using System;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;

namespace Pgd.Wsi.Data
{
    public static class ShippingConfirmations
    {
        /// <summary>
        /// Retrieves recently updated shipping confirmations.
        /// </summary>
        /// <param name="connectionString">Open SqlConnection to the WSI database</param>
        /// <exception cref="ArgumentException">An exception is thrown in no pick ticket number is found.</exception>
        /// <returns>Shipping comfirmations with empty line items</returns>
        public static ShippingConfirmationModel GetShippingConfirmation(string pickTicketNumber, string connectionString)
        {
            using SqlConnection conn = new(connectionString);

            try
            {
                conn.Open();

                using SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT * 
                    FROM shipping_confirmation
                    WHERE [shipping_confirmation].[pick_ticket_number] = @number
                    ORDER BY created_at DESC";
                cmd.Parameters.AddWithValue("@number", pickTicketNumber);

                using SqlDataReader reader = cmd.ExecuteReader();

                if (!reader.HasRows)
                {
                    return null;
                }

                int pickTicketNumberIdx = reader.GetOrdinal("pick_ticket_number");
                int trackingNumber = reader.GetOrdinal("tracking_number");
                int shipDate = reader.GetOrdinal("ship_date");
                int shippingMethod = reader.GetOrdinal("shipping_method");
                int createdAtIdx = reader.GetOrdinal("created_at");
                int updatedAtIdx = reader.GetOrdinal("updated_at");

                reader.Read();
                
                ShippingConfirmationModel shippingConfirmation = new()
                {
                    PickTicketNumber = reader.GetString(pickTicketNumberIdx),
                    LineItems = new(),
                    ShipDate = reader.GetDateTime(shipDate),
                    ShippingMethod = reader.GetString(shippingMethod),
                    TrackingNumber = reader.GetString(trackingNumber),
                    CreatedAt = reader.GetDateTime(createdAtIdx),
                    UpdatedAt = reader.GetDateTime(updatedAtIdx)
                };

                PickTicketModel pickTicket = PickTicket.GetPickTicket(pickTicketNumber, conn.ConnectionString);

                // Check to see if pickticket exists, if not, throw exception
                if (pickTicket == null)
                {
                    throw new ArgumentException("Pickticket number cannot be found in database");
                }

                List<ShippingConfirmationDetailModel> shippingDetails = new();

                pickTicket.LineItems.ForEach(detail =>
                {
                    shippingConfirmation.LineItems.Add(new ShippingConfirmationDetailModel()
                    {
                        LineNumber = detail.LineNumber,
                        Sku = detail.Sku,
                        Units = detail.Units
                    });
                });

                return shippingConfirmation;
            }
            catch
            {
                throw;
            }
            finally
            {
                conn.Close();
            }
            
        }

        /// <summary>
        /// Inserts a shipping confirmation into the database.
        /// </summary>
        /// <param name="shippingConfirmation">Shipping confirmation to be inserted</param>
        /// <param name="connectionString">Connection string to SQL Server instance</param>
        /// <exception cref="Exception">All operations around this method are surrounded by a transaction which rollbacked if an exception is encountered, commited otherwise.</exception>
        public static void InsertShippingConfirmation(ShippingConfirmationModel shippingConfirmation, string connectionString)
        {
            using SqlConnection conn = new(connectionString);
            using SqlTransaction transaction = conn.BeginTransaction();

            try
            {
                ShippingConfirmationModel confirmation = new()
                {
                    PickTicketNumber = shippingConfirmation.PickTicketNumber,
                    TrackingNumber = shippingConfirmation.TrackingNumber,
                    LineItems = shippingConfirmation.LineItems,
                    CreatedAt = shippingConfirmation.CreatedAt,
                    UpdatedAt = shippingConfirmation.UpdatedAt,
                    ShipDate = shippingConfirmation.ShipDate,
                    ShippingMethod = shippingConfirmation.ShippingMethod,
                };

                InsertConfrimation(confirmation, conn, transaction);

                shippingConfirmation.LineItems.ForEach(line =>
                {
                    InsertLineItems(line, conn, transaction);
                });

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                conn.Close();
            }

        }

        private static void InsertConfrimation(ShippingConfirmationModel detail, SqlConnection conn, SqlTransaction transaction)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"INSERT INTO [shipping_confirmation] (pick_ticket_number, ship_date, tracking_number, shipping_method, created_at, updated_at)
            VALUES (@pick_ticket_number, @ship_date, @tracking_number, @shipping_method, @created_at, @updated_at);";

            cmd.Parameters.AddWithValue("@pick_ticket_number", detail.PickTicketNumber);
            cmd.Parameters.AddWithValue("@ship_date", detail.ShipDate);
            cmd.Parameters.AddWithValue("@tracking_number", detail.TrackingNumber);
            cmd.Parameters.AddWithValue("@shipping_method", detail.ShippingMethod);
            cmd.Parameters.AddWithValue("@created_at", detail.CreatedAt);
            cmd.Parameters.AddWithValue("@updated_at", detail.UpdatedAt);

            cmd.ExecuteScalar();
        }

        private static void InsertLineItems(ShippingConfirmationDetailModel item, SqlConnection conn, SqlTransaction transaction)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"INSERT INTO [shipping_confirmation] (line_number, sku, units)
            VALUES (@line_number, @sku, @quantity);";

            cmd.Parameters.AddWithValue("@line_number", item.LineNumber);
            cmd.Parameters.AddWithValue("@sku", item.Sku);
            cmd.Parameters.AddWithValue("quantity", item.Units);

            cmd.ExecuteScalar();
        }

    }
}
