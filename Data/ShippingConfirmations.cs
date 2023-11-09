using Microsoft.Data.SqlClient;
using Pgd.Wsi.Models.PickTicket;
using System;
using System.Collections.Generic;
using Pgd.Wsi.Models.ShippingConfirmation;

namespace Pgd.Wsi.Data
{
    public static class ShippingConfirmations
    {
        /// <summary>
        /// Retrieves recently updated shipping confirmations
        /// </summary>
        /// <param name="conn">Open SqlConnection to the WSI database</param>
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
                    WHERE [pt_detail].[pick_ticket_number] = @number
                    ORDER BY created_at DESC";
                cmd.Parameters.AddWithValue("@number", pickTicketNumber);

                using SqlDataReader reader = cmd.ExecuteReader();

                if (!reader.HasRows)
                {
                    return null;
                }

                int pickTicketNumberIdx = reader.GetOrdinal("pick_ticket_number");
                int trackingNumber = reader.GetOrdinal("tracking_number");
                int createdAtIdx = reader.GetOrdinal("created_at");
                int updatedAtIdx = reader.GetOrdinal("updated_at");

                reader.Read();
                
                ShippingConfirmationModel shippingConfirmation = new()
                {
                    PickTicketNumber = reader.GetString(pickTicketNumberIdx),
                    LineItems = new(),
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

    }
}
