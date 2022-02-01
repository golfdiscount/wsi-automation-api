"""
Sends WSI picktickets that are in the blob storage container to WSI's filesystem
"""
import azure.functions as func
import datetime
import logging
import mysql.connector as sql
import os
import paramiko as pm
import tempfile

from paramiko import AutoAddPolicy
from paramiko.sftp_client import SFTPClient
from wsi.pickticket import Pickticket

def main(blob: func.InputStream) -> None:
    """Processes a blob with order information and:
    1) Inserts into WSI database
    2) SFTPs to WSI"""

    logging.info(f'Processing {blob.name}')

    ticket = Pickticket()
    ticket.read_csv(blob.read())

    db_cnx: sql.MySQLConnection = sql.connect(
        user=os.environ['db_user'],
        password=os.environ['db_pass'],
        host=os.environ['db_host'],
        database=os.environ['db_database']
    )
    cursor = db_cnx.cursor()
    try:
        for order in ticket:
            insert_db(cursor, order)
    except Exception as e:
        db_cnx.rollback()
        logging.error(e)
        traceback = e.__traceback__
        while traceback:
            logging.error("{}: {}".format(traceback.tb_frame.f_code.co_filename, traceback.tb_lineno))
            traceback = traceback.tb_next
    else:
        db_cnx.commit()
    finally:
        db_cnx.close()

    with tempfile.TemporaryFile() as order:
        order.write(blob.read())
        order.seek(0)
        upload_sftp(order)

def upload_db():
    pass

def insert_db(cursor, order: dict) -> None:
    """Inserts an order into the database

    Insertion order:
    1) Customer
    2) Recipient
    3) Order
    4) Product
    5) Line item

    Args:
        cursor: mysql.connection cursor used to interact with the database
        order: dict containing order information

    Raises:
        ValueError: The SKU for a product could not be found
    """
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
        'order_num': order['order_num'],
        'sold_to': customer_id,
        'ship_to': recipient_id,
        'ship_method': order['shipping_method'],
        'order_date': order['order_date']
    })

    line = 0
    for product in order['products']:
        line += 1
        insert_product(cursor, {
            'sku': product['sku'],
            'unit_price': product['price']
        })
        insert_line_item(cursor, {
            'pick_ticket_num': f'C{order["order_num"]}',
            'line_num': line,
            'units_to_ship': product['quantity'],
            'quantity': product['quantity'],
            'sku': product['sku']
        })

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
        STR_TO_DATE("{order["order_date"]}", "%m/%d/%Y")
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
        unit_price
    ) VALUES (
        "{product["sku"]}",
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

def upload_sftp(order: tempfile._TemporaryFileWrapper):
    """Uploads an order to WSI's file system in
    the directory specified by the "target"
    environment variable

    Args:
        order: A TemporaryFile containing order information in WSI's CSV format
    """
    with pm.SSHClient() as client:
        client.set_missing_host_key_policy(AutoAddPolicy())

        logging.info('Connecting to WSI server...')
        client.connect(os.environ['WSI_HOST'],
            username=os.environ['WSI_USER'],
            password=os.environ['WSI_PASS'])
        logging.info('Connection successful')

        with SFTPClient.from_transport(client.get_transport()) as sftp:
            now = datetime.datetime.now()
            date_string = now.strftime(f"%m_%d_%Y_%H_%M_%S")
            file_name = f'PT_WSI_{date_string}.csv'

            logging.info(f'Uploading {file_name} to {os.environ["target"]} directory')
            remote_path = os.path.join(os.environ["target"], file_name)
            sftp.putfo(order, remote_path, confirm=False)
            logging.info('SFTP finished successfully')
