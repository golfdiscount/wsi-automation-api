import logging
import mysql.connector
import azure.functions as func
from . import wsi
from .config import config
from .Requests import Requester
from .ticket.Pickticket import Pickticket


def main(req: func.HttpRequest) -> func.HttpResponse:
    logging.info('Python HTTP trigger function processed a request.')

    try:
        file = req.files.get('file')

        cnx = mysql.connector.connect(user=config["mysql"]["user"],
                                    password=config["mysql"]["pass"],
                                    host=config["mysql"]["host"],
                                    database=config["mysql"]["database"])
        cursor = cnx.cursor()

        logging.info("")
        logging.info("ShipStation API requester initializing...")
        logging.info("")
        requester = Requester("https://ssapi.shipstation.com", "ssapi.shipstation.com")
        requester.encode_base64("3b72e28b4eb547ab976cc0ac8b1a0662", "fe2bbc64d7de426c8c298b4107dac60a")

        pick_ticket = Pickticket(file)

        orders = pick_ticket.get_orders()
        orders = add_sku_names(orders, requester)

        upload_to_api(cursor, orders)

        logging.info("Committing data to database...")
        cnx.commit()
        logging.info("Data commited")
        logging.info("Closing connection to database...")
        cnx.close()
        logging.info("Connection to database closed")

        return func.HttpResponse(f"The file {file.filename} has uploaded successfully")
    except Exception as e:
        logging.info(e.args)


def add_sku_names(orders, requester):
    """
    Adds SKU names to all pick ticket details

    @type orders: dict
    @param orders: Set of headers and details
    @type requester: Requester object
    @param requester: Requester object used to make requests to ShipStation API
    @rtype: dict
    @return: A set of headers and details with sku names added
    """
    for ticket in orders:
        details = orders[ticket]["details"]
        for detail in details:
            sku = details[detail].get_sku()
            details[detail].set_sku_name(get_sku_name(requester.get("/products", {"sku": sku}), sku))

    return orders


def get_sku_name(response, target):
    """
    Gets a target sku from a set of SKUs received from ShipStation

    @type response: dict
    @param response: List of products from ShipStation
    @type target: String
    @param target: Target string to be searched for
    @rtype: String or None
    @return: The matching SKU or None if it cannot be found
    """
    for product in response["products"]:
        if product["sku"] == str(target):
            return product["name"]
    return None


def upload_to_api(cursor, orders):
    """
    Breaks down a header and a detail for each order and inserts them into the database

    @type cursor: A mysql.connector cursor
    @param cursor: Cursor corresponding to the WSI orders database
    @type orders: dict
    @param orders: A set of orders on the pick ticket
    """
    for ticket in orders:
        header = orders[ticket]["header"]
        logging.info(f"Uploading order {header.get_pick_num()}")
        logging.info("")
        upload_header(cursor, header)

        for detail in orders[ticket]["details"]:
            upload_detail(cursor, orders[ticket]["details"][detail])


def upload_header(cursor, header):
    """
    Uploads information from a header to the WSI API

    @type cursor: A mysql.connector cursor
    @param cursor: Cursor corresponding to the WSI orders database
    @type header: Pick ticket header object
    @param header: The header you want to upload
    """
    # Add customer information
    sold_to_id = wsi.add_cus(cursor, header.get_cus_info())

    # Add recipient information
    ship_to_id = wsi.add_rec(cursor, header.get_rec_info())

    # Add order information
    order_info = header.get_order_info()
    wsi.add_order(cursor, {
        "pick_ticket_num": order_info["pick_ticket_num"],
        "order_num": order_info["order_num"],
        "sold_to": sold_to_id,
        "ship_to": ship_to_id,
        "ship_method": order_info["ship_method"],
        "order_date": order_info["order_date"]
    })


def upload_detail(cursor, detail):
    """
    Uploads information from a detail record to the WSI API

    @type cursor: A mysql.connector cursor
    @param cursor: Cursor corresponding to the WSI orders database
    @type detail: A pick ticket detail object
    @param detail: Detail record to be uploaded
    """
    detail = detail.get_pick_details()
    # Add product
    wsi.add_product(cursor, {
        "sku": detail["sku"],
        "sku_name": detail["sku_name"],
        "unit_price": detail["unit_price"]
    })

    # Add line item
    wsi.add_lt(cursor, {
        "pick_ticket_num": detail["pick_ticket_num"],
        "line_num": detail["line_num"],
        "units_to_ship": detail["units_to_ship"],
        "sku": detail["sku"],
        "quantity": detail["quantity"]
    })
