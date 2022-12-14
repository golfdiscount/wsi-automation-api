using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using WsiApi.Models.PurchaseOrder;

namespace WsiApi.Data
{
    public static class PurchaseOrder
    {
        /// <summary>
        /// Retrieves a purchase order from the database
        /// </summary>
        /// <param name="purchaseOrderNumber">Number of the purchase order</param>
        /// <param name="connString">Connection string to database</param>
        /// <returns>Purchase order information with line items</returns>
        public static List<PurchaseOrderModel> GetPurchaseOrder(string purchaseOrderNumber, string connString)
        {
            using SqlConnection conn = new(connString);
            conn.Open();
            
            List<PurchaseOrderModel> purchaseOrders = GetPurchaseOrderHeader(purchaseOrderNumber, conn);

            if (purchaseOrders.Count == 0)
            {
                return null;
            }

            purchaseOrders.ForEach(purchaseOrder =>
            {
                purchaseOrder.LineItems = GetPurchaseOrderDetail(purchaseOrderNumber, conn);
            });

            return purchaseOrders;
        }

        private static List<PurchaseOrderModel> GetPurchaseOrderHeader(string purchaseOrderNumber, SqlConnection conn)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT * FROM po_header WHERE po_number = @po_number;";
            cmd.Parameters.AddWithValue("@po_number", purchaseOrderNumber);

            using SqlDataReader reader = cmd.ExecuteReader();

            int purchaseOrderNumberIdx = reader.GetOrdinal("po_number");
            int actionIdx = reader.GetOrdinal("action");
            int createdAtIdx = reader.GetOrdinal("created_at");
            int updatedAtIdx = reader.GetOrdinal("updated_at");

            List<PurchaseOrderModel> purchaseOrders = new();

            while(reader.Read())
            {
                PurchaseOrderModel purchaseOrder = new()
                {
                    PoNumber = reader.GetString(purchaseOrderNumberIdx),
                    Action = reader.GetString(actionIdx)[0],
                    CreatedAt = reader.GetDateTime(createdAtIdx),
                    UpdatedAt = reader.GetDateTime(updatedAtIdx),
                    LineItems = new()
                };

                purchaseOrders.Add(purchaseOrder);
            }

            return purchaseOrders;
        }

        private static List<PurchaseOrderDetailModel> GetPurchaseOrderDetail(string purchaseOrderNumber, SqlConnection conn)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT * FROM po_detail WHERE po_number = @po_number ORDER BY line_number ASC;";
            cmd.Parameters.AddWithValue("@po_number", purchaseOrderNumber);

            using SqlDataReader reader = cmd.ExecuteReader();

            int purchaseOrderNumberIdx = reader.GetOrdinal("po_number");
            int lineNumberIdx = reader.GetOrdinal("line_number");
            int actionIdx = reader.GetOrdinal("action");
            int skuIdx = reader.GetOrdinal("sku");
            int unitsIdx = reader.GetOrdinal("units");
            int createdAtIdx = reader.GetOrdinal("created_at");
            int updatedAtIdx = reader.GetOrdinal("updated_at");

            List<PurchaseOrderDetailModel> details = new();

            while(reader.Read())
            {
                PurchaseOrderDetailModel detail = new()
                {
                    PoNumber = reader.GetString(purchaseOrderNumberIdx),
                    LineNumber = reader.GetInt32(lineNumberIdx),
                    Action = reader.GetString(actionIdx)[0],
                    Sku = reader.GetString(skuIdx),
                    Units = reader.GetInt32(unitsIdx),
                    CreatedAt = reader.GetDateTime(createdAtIdx),
                    UpdatedAt = reader.GetDateTime(updatedAtIdx)
                };

                details.Add(detail);
            }

            return details;

        }

        /// <summary>
        /// Inserts a purchase order into the database
        /// </summary>
        /// <param name="purchaseOrder">Purchase order and line items to be inserted</param>
        /// <param name="connString">Connection string to database</param>
        public static void InsertPurchaseOrder(PurchaseOrderModel purchaseOrder, string connString)
        {
            using SqlConnection conn = new(connString);
            conn.Open();
            using SqlTransaction transaction = conn.BeginTransaction();

            try
            {
                InsertPurchaseOrderHeader(purchaseOrder, conn, transaction);   

                foreach (PurchaseOrderDetailModel detail in purchaseOrder.LineItems)
                {
                    InsertPurchaseOrderDetail(detail, conn, transaction);
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

        /// <summary>
        /// Inserts purchase order header information into the database
        /// </summary>
        /// <param name="purchaseOrder">Purchase order to be inserted</param>
        /// <param name="conn">Open SqlConnection to database</param>
        /// <param name="transaction">Transaction associated with the current connection</param>
        private static void InsertPurchaseOrderHeader(PurchaseOrderModel purchaseOrder, SqlConnection conn, SqlTransaction transaction)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"IF NOT EXISTS (SELECT * 
                        FROM po_header 
                        WHERE po_header.po_number = @po_number)
                    BEGIN
                        INSERT INTO po_header (po_number)
                        VALUES (@po_number);
                    END
                    ELSE
                    BEGIN
                        UPDATE po_header
                        SET updated_at = CURRENT_TIMESTAMP
                        WHERE po_number = '@po_number';
                    END";
            cmd.Parameters.AddWithValue("@po_number", purchaseOrder.PoNumber);
            cmd.ExecuteScalar();
        }

        /// <summary>
        /// Inserts a purchase order line item into the database
        /// </summary>
        /// <param name="purchaseOrderDetail">Purchase order detail item to be inserted</param>
        /// <param name="conn">Open SqlConnection to database</param>
        /// <param name="transaction">Transaction associated with the current connection</param>
        private static void InsertPurchaseOrderDetail(PurchaseOrderDetailModel purchaseOrderDetail, SqlConnection conn, SqlTransaction transaction)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"IF NOT EXISTS (SELECT * 
	                    FROM po_detail 
	                    WHERE po_detail.po_number = @po_number 
	                    AND po_detail.line_number = @line_number)
                    BEGIN
	                    INSERT INTO po_detail (po_number, line_number, sku, units)
	                    VALUES (@po_number, @line_number, @sku, @units);
                    END
                    ELSE
                    BEGIN
	                    UPDATE po_detail
	                    SET updated_at = CURRENT_TIMESTAMP
	                    WHERE po_detail.po_number = @po_number AND po_detail.line_number = @line_number;
                    END"
            ;

            cmd.Parameters.AddWithValue("@po_number", purchaseOrderDetail.PoNumber);
            cmd.Parameters.AddWithValue("@line_number", purchaseOrderDetail.LineNumber);
            cmd.Parameters.AddWithValue("@sku", purchaseOrderDetail.Sku);
            cmd.Parameters.AddWithValue("@units", purchaseOrderDetail.Units);

            cmd.ExecuteScalar();
        }
    }
}
