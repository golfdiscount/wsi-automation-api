using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using WsiApi.Models;

namespace WsiApi.Data
{
    public static class PickTicket
    {
        public static List<PickTicketModel> GetPickTicket(string connString)
        {
            using SqlConnection conn = new(connString);
            conn.Open();

            List<PickTicketModel> pickTickets = new();

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT TOP 30 * FROM [pt_header] ORDER BY [created_at] DESC";

            using SqlDataReader reader = cmd.ExecuteReader();

            int pickticketNumberIdx = reader.GetOrdinal("pick_ticket_number");
            int orderNumberIdx = reader.GetOrdinal("order_number");
            int actionIdx = reader.GetOrdinal("action");
            int storeIdx = reader.GetOrdinal("store");
            int customerIdx = reader.GetOrdinal("customer");
            int recipientIdx = reader.GetOrdinal("recipient");
            int shippingMethodIdx = reader.GetOrdinal("shipping_method");
            int orderDateIdx = reader.GetOrdinal("order_date");
            int channelIdx = reader.GetOrdinal("channel");
            int createdIdx = reader.GetOrdinal("created_at");
            int updatedIdx = reader.GetOrdinal("updated_at");

            List<HeaderModel> headers = new();

            while (reader.Read())
            {
                HeaderModel header = new()
                {
                    PickTicketNumber = reader.GetString(pickticketNumberIdx),
                    OrderNumber = reader.GetString(orderNumberIdx),
                    Action = reader.GetString(actionIdx)[0], // Microsoft.Data.SqlClient.SqlDataReader.GetChar() is not supported
                    Store = reader.GetInt32(storeIdx),
                    Customer = reader.GetInt32(customerIdx),
                    Recipient = reader.GetInt32(recipientIdx),
                    ShippingMethod = reader.GetString(shippingMethodIdx),
                    OrderDate = reader.GetDateTime(orderDateIdx),
                    Channel = reader.GetInt32(channelIdx),
                    CreatedAt = reader.GetDateTime(createdIdx),
                    UpdatedAt = reader.GetDateTime(updatedIdx)
                };

                headers.Add(header);
            }

            reader.Close();

            foreach (HeaderModel header in headers)
            {
                List<DetailModel> details = GetDetail(header.PickTicketNumber, conn);

                AddressModel customer = GetAddress(header.Customer, conn);
                AddressModel recipient = GetAddress(header.Recipient, conn);

                PickTicketModel ticket = new()
                {
                    PickTicketNumber = header.PickTicketNumber,
                    OrderNumber = header.OrderNumber,
                    Action = header.Action,
                    Store = header.Store,
                    Customer = customer,
                    Recipient = recipient,
                    ShippingMethod = header.ShippingMethod,
                    LineItems = details,
                    OrderDate = header.OrderDate,
                    Channel = header.Channel,
                    CreatedAt = header.CreatedAt,
                    UpdatedAt = header.UpdatedAt,
                };

                pickTickets.Add(ticket);
            }

            return pickTickets;
        }

        public static List<PickTicketModel> GetPickTicket(string orderNumber, string connString)
        {
            using SqlConnection conn = new(connString);
            conn.Open();

            try
            {
                List<PickTicketModel> pickTickets = new();
                List<HeaderModel> headers = GetHeader(orderNumber, conn);

                foreach (HeaderModel header in headers)
                {
                    List<DetailModel> details = GetDetail(header.PickTicketNumber, conn);

                    AddressModel customer = GetAddress(header.Customer, conn);
                    AddressModel recipient = GetAddress(header.Recipient, conn);

                    PickTicketModel ticket = new()
                    {
                        PickTicketNumber = header.PickTicketNumber,
                        OrderNumber = orderNumber,
                        Action = header.Action,
                        Store = header.Store,
                        Customer = customer,
                        Recipient = recipient,
                        ShippingMethod = header.ShippingMethod,
                        LineItems = details,
                        OrderDate = header.OrderDate,
                        Channel = header.Channel,
                        CreatedAt = header.CreatedAt,
                        UpdatedAt = header.UpdatedAt,
                    };

                    pickTickets.Add(ticket);
                }

                return pickTickets;
            } catch
            {
                throw;
            }
            finally
            {
                conn.Close();
            }
        }

        public static void InsertPickTicket(PickTicketModel pickTicket, string connString)
        {
            using SqlConnection conn = new(connString);
            conn.Open();
            using SqlTransaction transaction = conn.BeginTransaction();

            try
            {
                int customerId = InsertAddress(pickTicket.Customer, conn, transaction);
                int recipientId;

                if (pickTicket.Customer.Equals(pickTicket.Recipient))
                {
                    recipientId = customerId;
                }
                else
                {
                    recipientId = InsertAddress(pickTicket.Recipient, conn, transaction);
                }

                HeaderModel orderHeader = new()
                {
                    PickTicketNumber = pickTicket.PickTicketNumber,
                    OrderNumber = pickTicket.OrderNumber,
                    Action = pickTicket.Action,
                    Store = pickTicket.Store,
                    Customer = customerId,
                    Recipient = recipientId,
                    ShippingMethod = pickTicket.ShippingMethod,
                    OrderDate = pickTicket.OrderDate,
                    Channel = pickTicket.Channel
                };

                InsertHeader(orderHeader, conn, transaction);

                pickTicket.LineItems.ForEach(line =>
                {
                    line.PickTicketNumber = orderHeader.PickTicketNumber;
                    line.UnitsToShip = line.Units;
                    line.Action = 'I';
                    InsertDetail(line, conn, transaction);
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

        private static List<HeaderModel> GetHeader(string orderNumber, SqlConnection conn)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT * FROM [pt_header] WHERE [pt_header].[order_number] = @number;";
            cmd.Parameters.AddWithValue("@number", orderNumber);

            using SqlDataReader reader = cmd.ExecuteReader();

            int pickticketNumberIdx = reader.GetOrdinal("pick_ticket_number");
            int orderNumberIdx = reader.GetOrdinal("order_number");
            int actionIdx = reader.GetOrdinal("action");
            int storeIdx = reader.GetOrdinal("store");
            int customerIdx = reader.GetOrdinal("customer");
            int recipientIdx = reader.GetOrdinal("recipient");
            int shippingMethodIdx = reader.GetOrdinal("shipping_method");
            int orderDateIdx = reader.GetOrdinal("order_date");
            int channelIdx = reader.GetOrdinal("channel");
            int createdIdx = reader.GetOrdinal("created_at");
            int updatedIdx = reader.GetOrdinal("updated_at");

            if (!reader.HasRows)
            {
                return null;
            }

            List<HeaderModel> headers = new();

            while(reader.Read())
            {
                HeaderModel header = new()
                {
                    PickTicketNumber = reader.GetString(pickticketNumberIdx),
                    OrderNumber = reader.GetString(orderNumberIdx),
                    Action = reader.GetString(actionIdx)[0], // Microsoft.Data.SqlClient.SqlDataReader.GetChar() is not supported
                    Store = reader.GetInt32(storeIdx),
                    Customer = reader.GetInt32(customerIdx),
                    Recipient = reader.GetInt32(recipientIdx),
                    ShippingMethod = reader.GetString(shippingMethodIdx),
                    OrderDate = reader.GetDateTime(orderDateIdx),
                    Channel = reader.GetInt32(channelIdx),
                    CreatedAt = reader.GetDateTime(createdIdx),
                    UpdatedAt = reader.GetDateTime(updatedIdx)
                };

                headers.Add(header);
            }

            return headers;
        }

        private static AddressModel GetAddress(int addressId, SqlConnection conn)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT * FROM [address] WHERE [address].[id] = @id";
            cmd.Parameters.AddWithValue("@id", addressId);

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

            return address;
        }

        private static List<DetailModel> GetDetail(string pickTicketNumber, SqlConnection conn)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT * FROM [pt_detail] WHERE [pt_detail].[pick_ticket_number] = @number";
            cmd.Parameters.AddWithValue("@number", pickTicketNumber);

            using SqlDataReader reader = cmd.ExecuteReader();

            int pickticketIdx = reader.GetOrdinal("pick_ticket_number");
            int lineNumberIdx = reader.GetOrdinal("line_number");
            int actionIdx = reader.GetOrdinal("action");
            int skuIdx = reader.GetOrdinal("sku");
            int unitsIdx = reader.GetOrdinal("units");
            int unitsToShipIdx = reader.GetOrdinal("units_to_ship");
            int createdIdx = reader.GetOrdinal("created_at");
            int updatedIdx = reader.GetOrdinal("updated_at");

            List<DetailModel> details = new();

            while (reader.Read())
            {
                DetailModel detail = new()
                {
                    PickTicketNumber = reader.GetString(pickticketIdx),
                    LineNumber = reader.GetInt32(lineNumberIdx),
                    Action = reader.GetString(actionIdx)[0],
                    Sku = reader.GetString(skuIdx),
                    Units = reader.GetInt32(unitsIdx),
                    UnitsToShip = reader.GetInt32(unitsToShipIdx),
                    Created_at = reader.GetDateTime(createdIdx),
                    Updated_at = reader.GetDateTime(updatedIdx)
                };

                details.Add(detail);
            }

            return details;
        }
    
        private static void InsertHeader(HeaderModel header, SqlConnection conn, SqlTransaction transaction)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"INSERT INTO [pt_header] (pick_ticket_number, order_number, store, customer, recipient, shipping_method, order_date, channel)
            VALUES (@pick_ticket_number, @order_number, @store, @customer, @recipient, @shipping_method, @order_date, @channel);";

            cmd.Parameters.Add("@pick_ticket_number", System.Data.SqlDbType.VarChar).Value = header.PickTicketNumber;
            cmd.Parameters.Add("@order_number", System.Data.SqlDbType.VarChar).Value = header.OrderNumber;
            cmd.Parameters.Add("@store", System.Data.SqlDbType.Int).Value = header.Store;
            cmd.Parameters.Add("@customer", System.Data.SqlDbType.Int).Value = header.Customer;
            cmd.Parameters.Add("@recipient", System.Data.SqlDbType.Int).Value = header.Recipient;
            cmd.Parameters.Add("@shipping_method", System.Data.SqlDbType.VarChar).Value = header.ShippingMethod;
            cmd.Parameters.Add("@order_date", System.Data.SqlDbType.Date).Value = header.OrderDate;
            cmd.Parameters.Add("@channel", System.Data.SqlDbType.Int).Value = header.Channel;

            cmd.ExecuteScalar();
        }
    
        private static int InsertAddress(AddressModel address, SqlConnection conn, SqlTransaction transaction)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"INSERT INTO [address] (name, street, city, state, country, zip)
            VALUES (@name, @street, @city, @state, @country, @zip);
            SELECT CONVERT(INT, SCOPE_IDENTITY());";

            cmd.Parameters.Add("@name", System.Data.SqlDbType.VarChar).Value = address.Name;
            cmd.Parameters.Add("@street", System.Data.SqlDbType.VarChar).Value = address.Street;
            cmd.Parameters.Add("@city", System.Data.SqlDbType.VarChar).Value = address.City;
            cmd.Parameters.Add("@state", System.Data.SqlDbType.VarChar).Value = address.State;
            cmd.Parameters.Add("@country", System.Data.SqlDbType.VarChar).Value = address.Country;
            cmd.Parameters.Add("@zip", System.Data.SqlDbType.VarChar).Value = address.Zip;

            object result = cmd.ExecuteScalar();
            int insertId = (int)result;

            return insertId;
        }
    
        private static void InsertDetail(DetailModel detail, SqlConnection conn, SqlTransaction transaction)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"INSERT INTO [pt_detail] (pick_ticket_number, line_number, action, sku, units, units_to_ship)
            VALUES (@pick_ticket_number, @line_number, @action, @sku, @quantity, @units_to_ship);";

            cmd.Parameters.AddWithValue("@pick_ticket_number", detail.PickTicketNumber);
            cmd.Parameters.AddWithValue("@line_number", detail.LineNumber);
            cmd.Parameters.AddWithValue("@sku", detail.Sku);
            cmd.Parameters.AddWithValue("@action", detail.Action);
            cmd.Parameters.AddWithValue("@quantity", detail.Units);
            cmd.Parameters.AddWithValue("@units_to_ship", detail.UnitsToShip);

            cmd.ExecuteScalar();
        }
    }
}
