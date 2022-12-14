using Microsoft.Data.SqlClient;
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
