using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;
using WsiApi.Models.PurchaseOrder;

namespace WsiApi.Data
{
    public static class PurchaseOrder
    {
        public static PurchaseOrderModel GetPurchaseOrder(string purchaseOrderNumber, string connString)
        {
            using SqlConnection conn = new(connString);
            conn.Open();

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM [po_header] WHERE [po_header].[po_number] = @po_number;";
            cmd.Parameters.AddWithValue("@po_number", purchaseOrderNumber);

            using SqlDataReader headerReader = cmd.ExecuteReader();

            if (!headerReader.HasRows)
            {
                return null;
            }

            int poNumberIdx = headerReader.GetOrdinal("po_number");
            int actionIdx = headerReader.GetOrdinal("action");
            int createdAtIdx = headerReader.GetOrdinal("created_at");
            int updatedAtIdx = headerReader.GetOrdinal("updated_at");

            headerReader.Read();

            PurchaseOrderModel purchaseOrder = new()
            {
                PoNumber = headerReader.GetString(poNumberIdx),
                Action = headerReader.GetString(actionIdx)[0],
                CreatedAt = headerReader.GetDateTime(createdAtIdx),
                UpdatedAt = headerReader.GetDateTime(updatedAtIdx),
                LineItems = new()
            };

            headerReader.Close();

            cmd.CommandText = "SELECT * FROM [po_detail] WHERE [po_detail].[po_number] = @po_number;";
            cmd.Parameters.AddWithValue("@po_number", purchaseOrderNumber);
            using SqlDataReader detailReader = cmd.ExecuteReader();

            poNumberIdx = detailReader.GetOrdinal("po_number");
            createdAtIdx = detailReader.GetOrdinal("created_at");
            updatedAtIdx = detailReader.GetOrdinal("updated_at");
            actionIdx = detailReader.GetOrdinal("action");

            int lineNumberIdx = detailReader.GetOrdinal("line_number");
            int skuIdx = detailReader.GetOrdinal("sku");
            int unitsIdx = detailReader.GetOrdinal("units");

            while (detailReader.Read())
            {
                purchaseOrder.LineItems.Add(new()
                {
                    PoNumber = detailReader.GetString(poNumberIdx),
                    Action = detailReader.GetString(actionIdx)[0],
                    LineNumber = detailReader.GetInt32(lineNumberIdx),
                    Sku = detailReader.GetString(skuIdx),
                    Units = detailReader.GetInt32(unitsIdx),
                    CreatedAt = detailReader.GetDateTime(createdAtIdx),
                    UpdatedAt = detailReader.GetDateTime(updatedAtIdx)
                });
            }

            detailReader.Close();

            return purchaseOrder;
        }

        public static void InsertPurchaseOrder(PurchaseOrderModel purchaseOrder, string connString)
        {
            using SqlConnection conn = new(connString);
            conn.Open();
            using SqlTransaction transaction = conn.BeginTransaction();

            try
            {
                using SqlCommand cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO [po_header] (po_number) VALUES (@po_number);";
                cmd.Parameters.AddWithValue("@po_number", purchaseOrder.PoNumber);

                cmd.ExecuteScalar();

                cmd.CommandText = @"INSERT INTO [po_detail] (po_number, line_number, sku, units)
                    VALUES (@po_number, @line_number, @sku, @units);";

                foreach (PurchaseOrderDetailModel detail in purchaseOrder.LineItems)
                {
                    cmd.Parameters.AddWithValue("@line_number", detail.LineNumber);
                    cmd.Parameters.AddWithValue("@sku", detail.Sku);
                    cmd.Parameters.AddWithValue("@units", detail.Units);

                    cmd.ExecuteScalar();
                }

                transaction.Commit();
            } catch
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                conn.Close();
            }
            
        }
    }
}
