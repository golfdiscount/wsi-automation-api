def add_cus(cursor, cus_info):
    """
    Adds a customer's information into the WSI database

    @type cursor: A mysql.connector cursor
    @param cursor: Cursor corresponding to the WSI orders database
    @type cus_info: dict
    @param cus_info: Customer's information such as name and address
    @rtype: int
    @return: Customer's id in the database
    """
    qry = f"""
    INSERT INTO customer(
        sold_to_name,
        sold_to_address,
        sold_to_city,
        sold_to_state,
        sold_to_country,
        sold_to_zip)
    VALUES (
        "{cus_info["sold_to_name"]}",
        "{cus_info["sold_to_address"]}",
        "{cus_info["sold_to_city"]}",
        "{cus_info["sold_to_state"]}",
        "{cus_info["sold_to_country"]}",
        "{cus_info["sold_to_zip"]}"
    );
    """

    cursor.execute(qry)
    return cursor.lastrowid

def add_rec(cursor, rec_info):
    """
    Adds a recipient's information into the WSI database

    @type cursor: A mysql.connector cursor
    @param cursor: Cursor corresponding to the WSI orders database
    @type rec_info: dict
    @param rec_info: Recipient's information such as name and address
    @rtype: int
    @return: The id for the recipient
    """
    qry = f"""
    INSERT INTO recipient(
        ship_to_name,
        ship_to_address,
        ship_to_city,
        ship_to_state,
        ship_to_country,
        ship_to_zip)
    VALUES (
        "{rec_info["ship_to_name"]}",
        "{rec_info["ship_to_address"]}",
        "{rec_info["ship_to_city"]}",
        "{rec_info["ship_to_state"]}",
        "{rec_info["ship_to_country"]}",
        "{rec_info["ship_to_zip"]}"
    );
    """

    cursor.execute(qry)
    return cursor.lastrowid

def add_order(cursor, order_info):
    """
    Adds order information to the database

    @type cursor: A mysql.connector cursor
    @param cursor: Cursor corresponding to the WSI orders database
    @type order_info: dict
    @param order_info Contains order information such as order number and date
    """
    qry = f"""
    INSERT INTO IGNORE wsi_order(
        pick_ticket_num,
        order_num,
        sold_to,
        ship_to,
        ship_method
        )
    VALUES (
        "{order_info["pick_ticket_num"]}",
        "{order_info["order_num"]}",
        {order_info["sold_to"]},
        {order_info["ship_to"]},
        "{order_info["ship_method"]}"
    );
    """

    cursor.execute(qry)

def add_product(cursor, product_info):
    """
    Adds a product to the database

    @type cursor: A mysql.connector cursor
    @param cursor: Cursor corresponding to the WSI orders database
    @type product_info: dict
    @param product_info: Contains information about the product such as SKU and name
    """
    qry = f"""
    INSERT IGNORE INTO product(
        sku,
        sku_name,
        unit_price)
    VALUES (
        "{product_info["sku"]}",
        "{product_info["sku_name"]}",
        {product_info["unit_price"]}
    );
    """

    cursor.execute(qry)

def add_lt(cursor, line_info):
    """
    Adds a line item to the database

    @type cursor: A mysql.connector cursor
    @param cursor: Cursor corresponding to the WSI orders database
    @type line_info: dict
    @param line_info: Information about the line item such as line number and product sku
    """
    qry = f"""
    INSERT INTO line_item(
        pick_ticket_num,
        line_num,
        units_to_ship,
        sku,
        quantity)
    VALUES (
        "{line_info["pick_ticket_num"]}",
        {line_info["line_num"]},
        {line_info["units_to_ship"]},
        "{line_info["sku"]}",
        {line_info["quantity"]}
    );
    """

    cursor.execute(qry)

def add_shipping():
    pass
