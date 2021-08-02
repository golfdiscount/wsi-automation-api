import logging
import mysql.connector
import azure.functions as func
from . import wsi
from .config import config
from .Requests import Requester
from .pickticket.pickticket import Ticket


def main(req: func.HttpRequest) -> func.HttpResponse:
    try:
        cnx = mysql.connector.connect(user=config["mysql"]["user"],
                                password=config["mysql"]["pass"],
                                host=config["mysql"]["host"],
                                database=config["mysql"]["database"])
        cursor = cnx.cursor()

        logging.info("ShipStation API requester initializing...")

        requester = Requester("https://ssapi.shipstation.com", "ssapi.shipstation.com")
        requester.encode_base64("3b72e28b4eb547ab976cc0ac8b1a0662", "fe2bbc64d7de426c8c298b4107dac60a")
    except Exception as e:
        return func.HttpResponse(f"There was an error connecting to either the database or ShipStation\n{str(e)}", status_code=500)

    
    file = req.files.get('file')

    if file is not None:
        upload_file(cursor, file, requester)
    else:
        json = req.get_json()
        try:
            upload_json(cursor, json, requester)
        except KeyError as e:
            return func.HttpResponse(f"The JSON request is malformed\n{e}", status_code=400)

    logging.info("Committing data to database...")
    cnx.commit()
    logging.info("Data commited")
    logging.info("Closing connection to database...")
    cnx.close()

    return func.HttpResponse(f"The order(s) have uploaded successfully.")

def upload_file(cursor: mysql.connector.connection, file, requester: Requester) -> None:
    pick_ticket = Ticket()
    pick_ticket.read_csv(file)

    orders = pick_ticket.get_orders()

    _upload_to_api(cursor, orders, requester)


def upload_json(cursor: mysql.connector.connection, json: dict, requester: Requester) -> None:
    """
    Uploads an order in JSON format to the WSI DB
    
    @type cursor: mysql.connector.connection
    @param cursor: Connection to the WSI database
    @type json: dict
    @param json: JSON data containing order information
    @type requester: Requester object
    @param requester: Connection to ShipStation API
    """
    logging.info(f"Uploading order {json['order_num']}")

    # Add customer information
    sold_to_id = wsi.add_cus(cursor, json)

    # Add recipient information
    ship_to_id = wsi.add_rec(cursor, json)

    # Add order information
    wsi.add_order(cursor, {
        "pick_ticket_num": json["pick_ticket_num"],
        "order_num": json["order_num"],
        "sold_to": sold_to_id,
        "ship_to": ship_to_id,
        "ship_method": json["ship_method"],
        "order_date": json["order_date"]
    })

    sku = json["sku"]
    json["sku_name"] = _get_sku_name(requester.get("/products", {"sku": sku}), sku)

    # Add product
    wsi.add_product(cursor, {
        "sku": json["sku"],
        "sku_name": json["sku_name"],
        "unit_price": json["unit_price"]
    })

    # Add line item
    wsi.add_lt(cursor, {
        "pick_ticket_num": json["pick_ticket_num"],
        "line_num": json["line_num"],
        "units_to_ship": json["units_to_ship"],
        "sku": json["sku"],
        "quantity": json["quantity"]
    })


def _upload_to_api(cursor, orders, requester):
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
        _upload_header(cursor, header)

        for detail in orders[ticket]["details"]:
            _upload_detail(cursor, orders[ticket]["details"][detail], requester)


def _upload_header(cursor: mysql.connector.connection, header) -> None:
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


def _upload_detail(cursor: mysql.connector.connection, detail, requester: Requester):
    """
    Uploads information from a detail record to the WSI API

    @type cursor: A mysql.connector cursor
    @param cursor: Cursor corresponding to the WSI orders database
    @type detail: A pick ticket detail object
    @param detail: Detail record to be uploaded
    """
    sku = detail.get_sku()
    detail.set_sku_name(_get_sku_name(requester.get("/products", {"sku": sku}), sku))
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


def _get_sku_name(response, target) -> str:
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