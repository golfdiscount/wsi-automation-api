import azure.functions as func
import logging
import mysql.connector as sql
import os

def main(req: func.HttpRequest) -> func.HttpResponse:
    """Inserts an order into the WSI database

    Args:
        req: azure.functions.HttpRequest with order information in JSON body

    Return:
        azure.functions.HttpResponse with status code indicating if insertion was successful
    """
    try:
        order: dict = req.get_json()
    except ValueError:
        return func.HttpResponse('Please submit order data in request', status_code=400)

    db_cnx: sql.MySQLConnection = sql.connect(
        user=os.environ['db_user'],
        password=os.environ['db_pass'],
        host=os.environ['db_host'],
        database=os.environ['db_database']
    )

    cursor = db_cnx.cursor()    

    """
    Insert order for the database is:
    1) Customer
    2) Recipient
    3) Order
    4) Product
    5) Line item
    """
    try:
        customer_id = insert_customer(cursor, {
            'sold_to_name': order['customer']['name'],
            'sold_to_address': order['customer']['address'],
            'sold_to_city': order['customer']['city'],
            'sold_to_state': order['customer']['state'],
            'sold_to_country': order['customer']['country'],
            'sold_to_zip': order['customer']['zip']
        })
        recipient_id = insert_recipient(cursor, {
            'ship_to_name': order['recipient']['name'],
            'ship_to_address': order['recipient']['address'],
            'ship_to_city': order['recipient']['city'],
            'ship_to_state': order['recipient']['state'],
            'ship_to_country': order['recipient']['country'],
            'ship_to_zip': order['recipient']['zip']
        })
        insert_order(cursor, {
            'order_num': order['orderNum'],
            'sold_to': customer_id,
            'ship_to': recipient_id,
            'ship_method': order['shippingMethod'],
            'order_date': order['orderDate']
        })

        line = 0
        for product in order['products']:
            line += 1
            # TODO: Fix sku name
            insert_product(cursor, {
                'sku': product['sku'],
                'sku_name': 'sample sku name',
                'unit_price': product['price']
            })
            insert_line_item(cursor, {
                'pick_ticket_num': f'C{order["orderNum"]}',
                'line_num': line,
                'units_to_ship': product['quantity'],
                'quantity': product['quantity'],
                'sku': product['sku']
            })
    except KeyError as e:
        db_cnx.rollback()
        traceback = e.__traceback__
        while traceback:
            logging.error("{}: {}".format(traceback.tb_frame.f_code.co_filename, traceback.tb_lineno))
            traceback = traceback.tb_next
        return func.HttpResponse('Please make sure to include all attributes for the order model', status_code=400)

    db_cnx.commit()
    return func.HttpResponse('Order submitted')

def insert_customer(cursor, customer: dict) -> int:
    """Adds a customer's information into the WSI database

    Args:
        cursor: mysql.connector cursor object used to insert information into database
        customer: dict containing customer information

    Return:
        The row id of the customer entry as an int
    """
    qry = f"""
    INSERT INTO customer(
        sold_to_name,
        sold_to_address,
        sold_to_city,
        sold_to_state,
        sold_to_country,
        sold_to_zip
    ) VALUES (
        "{customer["sold_to_name"]}",
        "{customer["sold_to_address"]}",
        "{customer["sold_to_city"]}",
        "{customer["sold_to_state"]}",
        "{customer["sold_to_country"]}",
        "{customer["sold_to_zip"]}"
    );
    """

    cursor.execute(qry)
    return cursor.lastrowid

def insert_recipient(cursor, recipient: dict) -> int:
    """Adds recipient information to the WSI database

    Args:
        cursor: mysql.connector cursor object used to insert information into the database
        recipient: dict containing recipient information

    Return:
        The row id of the inserted recipient
    """
    qry = f"""
    INSERT INTO recipient (
        ship_to_name,
        ship_to_address,
        ship_to_city,
        ship_to_state,
        ship_to_country,
        ship_to_zip
    ) VALUES (
        "{recipient["ship_to_name"]}",
        "{recipient["ship_to_address"]}",
        "{recipient["ship_to_city"]}",
        "{recipient["ship_to_state"]}",
        "{recipient["ship_to_country"]}",
        "{recipient["ship_to_zip"]}"
    );
    """

    cursor.execute(qry)
    return cursor.lastrowid

def insert_order(cursor, order: dict) -> None:
    """Adds an order into the WSI database

    Args:
        cursor: mysql.connector cursor object used to insert information into the database
        order: dict containing order information
    """
    print(order['order_date'])
    qry = f"""
    INSERT IGNORE INTO wsi_order (
        pick_ticket_num,
        order_num,
        sold_to,
        ship_to,
        ship_method,
        order_date
    ) VALUES (
        "C{order["order_num"]}",
        "{order["order_num"]}",
        {order["sold_to"]},
        {order["ship_to"]},
        "{order["ship_method"]}",
        "{order["order_date"]}"
    ) ON DUPLICATE KEY UPDATE last_updated = CURRENT_TIMESTAMP;
    """

    cursor.execute(qry)

def insert_product(cursor, product: dict) -> None:
    """Adds a product to the WSI database, updating timestamp if already present

    Args:
        cursor: mysql.connector cursor object used to insert information into the database
        product: dict of product information
    """
    qry = f"""
    INSERT INTO product (
        sku,
        sku_name,
        unit_price
    ) VALUES (
        "{product["sku"]}",
        "{product["sku_name"]}",
        {product["unit_price"]}
    ) ON DUPLICATE KEY UPDATE last_used = CURRENT_TIMESTAMP;
    """

    cursor.execute(qry)

def insert_line_item(cursor, line: dict) -> None:
    """Adds a line item entry to the database, updating the timestamp if already present

    Args:
        cursor: mysql.connector cursor object used to insert information into the database
        line: dict containing information about the line item
    """
    qry = f"""
    INSERT INTO line_item (
        pick_ticket_num,
        line_num,
        units_to_ship,
        sku,
        quantity
    ) VALUES (
        "{line["pick_ticket_num"]}",
        {line["line_num"]},
        {line["units_to_ship"]},
        "{line["sku"]}",
        {line["quantity"]}
    ) ON DUPLICATE KEY UPDATE last_updated = CURRENT_TIMESTAMP;
    """

    cursor.execute(qry)
