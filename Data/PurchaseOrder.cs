using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using Pgd.Wsi.Models.PurchaseOrder;

namespace Pgd.Wsi.Data
{
    public static class PurchaseOrder
    {
        /// <summary>
        /// Returns the five most recently updated purchase orders
        /// </summary>
        /// <param name="connString">Connection string to the WSI database</param>
        /// <returns>A list of recently updated purchase orders</returns>
        public static List<PurchaseOrderModel> GetPurchaseOrder(string connString)
        {
            using SqlConnection conn = new(connString);
            conn.Open();

            List<PurchaseOrderModel> purchaseOrders = GetPurchaseOrderHeader(conn);

            foreach (PurchaseOrderModel purchaseOrder in purchaseOrders)
            {
                purchaseOrder.LineItems = GetPurchaseOrderDetail(purchaseOrder.PoNumber, conn);
            }

            return purchaseOrders;
        }

        /// <summary>
        /// Retrieves the five most recently updated purchase order headers
        /// </summary>
        /// <param name="conn">Open SqlConnection to the WSI database</param>
        /// <returns>A list of purchase orders with an emtpy list of line items</returns>
        private static List<PurchaseOrderModel> GetPurchaseOrderHeader(SqlConnection conn)
        {
            List<PurchaseOrderModel> purchaseOrders = new();

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT TOP 5 * 
                FROM po_header
                ORDER BY updated_at DESC;";

            using SqlDataReader reader = cmd.ExecuteReader();

            int purchaseOrderNumberIdx = reader.GetOrdinal("po_number");
            int actionIdx = reader.GetOrdinal("action");
            int createdAtIdx = reader.GetOrdinal("created_at");
            int updatedAtIdx = reader.GetOrdinal("updated_at");

            while (reader.Read())
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

        /// <summary>
        /// Retrieves a purchase order from the database
        /// </summary>
        /// <param name="purchaseOrderNumber">Number of the purchase order</param>
        /// <param name="connString">Connection string to database</param>
        /// <returns>Purchase order information with line items</returns>
        public static PurchaseOrderModel GetPurchaseOrder(string purchaseOrderNumber, string connString)
        {
            using SqlConnection conn = new(connString);
            conn.Open();
            
            PurchaseOrderModel purchaseOrder = GetPurchaseOrderHeader(purchaseOrderNumber, conn);

            if (purchaseOrder == null)
            {
                return null;
            }

            purchaseOrder.LineItems = GetPurchaseOrderDetail(purchaseOrderNumber, conn);

            return purchaseOrder;
        }

        /// <summary>
        /// Retrieves a singular purchase order header from the database
        /// </summary>
        /// <param name="purchaseOrderNumber">Purchase order to search for</param>
        /// <param name="conn">Open SqlConnection to the WSI database</param>
        /// <returns>A purchase order with an empty list of line items or null if a purchase order was not found</returns>
        private static PurchaseOrderModel GetPurchaseOrderHeader(string purchaseOrderNumber, SqlConnection conn)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT * FROM po_header WHERE po_number = @po_number;";
            cmd.Parameters.AddWithValue("@po_number", purchaseOrderNumber);

            using SqlDataReader reader = cmd.ExecuteReader();

            int purchaseOrderNumberIdx = reader.GetOrdinal("po_number");
            int actionIdx = reader.GetOrdinal("action");
            int createdAtIdx = reader.GetOrdinal("created_at");
            int updatedAtIdx = reader.GetOrdinal("updated_at");

            if (!reader.HasRows)
            {
                return null;
            }

            reader.Read();

            PurchaseOrderModel purchaseOrder = new()
            {
                PoNumber = reader.GetString(purchaseOrderNumberIdx),
                Action = reader.GetString(actionIdx)[0],
                CreatedAt = reader.GetDateTime(createdAtIdx),
                UpdatedAt = reader.GetDateTime(updatedAtIdx),
                LineItems = new()
            };

            return purchaseOrder;
        }

        /// <summary>
        /// Retrives detail records (line items) for a purchase order from the database
        /// </summary>
        /// <param name="purchaseOrderNumber">Purchase order number to search for</param>
        /// <param name="conn">Open SqlConnection to WSI database</param>
        /// <returns>List of </returns>
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
