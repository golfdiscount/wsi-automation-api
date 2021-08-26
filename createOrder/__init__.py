import logging
import requests
import os
import azure.functions as func
from .pickticket.pickticket import Ticket

async def main():
    return func.HttpResponse(requests.get('ifconfig.me'))

"""
def main(req: func.HttpRequest) -> func.HttpResponse:
    try:
        header = createHeader(req)
        detail = createDetail(req)
    except Exception as e:
        logging.warning(f"There was an error creating the ticket: {e}")
        return func.HttpResponse(f"There was an error creating the ticket\n{e}", status_code=500)

    ticket = Ticket()
    ticket.create_ticket(header, detail)

    logging.info(f"Order {header['order_num']} successfully created")
    logging.info(f"Attempting to upload order {header['order_num']} now...")

    try:
        res = requests.post(os.environ['FUNCTIONS_URL'], data=bytes(str(ticket), "utf-8"))

        if res.status_code != 200:
            res.raise_for_status()

        logging.info(f"Successfully uploaded order {header['order_num']} to the database")
    except Exception as e:
        return func.HttpResponse(f"There was an error uploading the order to the database\n{e}", status_code=500)

    return func.HttpResponse(bytes(str(ticket), "utf-8"), mimetype="text/plain")

def createHeader(req: func.HttpRequest) -> dict:
    Creates a header object to be used in a WSI pick ticket file
    header = {}

    header["pick_ticket_num"] = f"C{req.form['order_num']}"
    header["order_num"] = req.form["order_num"]
    header["order_date"] = req.form["order_date"]

    # Customer information
    header["sold_to_name"] = req.form["sold_to_name"]
    header["sold_to_address"] = req.form["sold_to_address"]
    header["sold_to_city"] = req.form["sold_to_city"]
    header["sold_to_state"] = req.form["sold_to_state"]
    header["sold_to_country"] = req.form["sold_to_country"]
    header["sold_to_zip"] = req.form["sold_to_zip"]

    # Recipient information
    if "ship_to_name" not in req.form.keys():
        header["ship_to_name"] = req.form["sold_to_name"]
        header["ship_to_address"] =req.form["sold_to_address"]
        header["ship_to_city"] = req.form["sold_to_city"]
        header["ship_to_state"] = req.form["sold_to_state"]
        header["ship_to_country"] = req.form["sold_to_country"]
        header["ship_to_zip"] = req.form["sold_to_zip"]
    else:
        header["ship_to_name"] = req.form["ship_to_name"]
        header["ship_to_address"] = req.form["ship_to_address"]
        header["ship_to_city"] = req.form["ship_to_city"]
        header["ship_to_state"] = req.form["ship_to_state"]
        header["ship_to_country"] = req.form["ship_to_country"]
        header["ship_to_zip"] = req.form["ship_to_zip"]

    header["ship_method"] = req.form["ship_method"]

    return header

def createDetail(req: func.HttpRequest) -> dict:
    detail = {}

    detail["pick_ticket_num"] = f"C{req.form['order_num']}"
    detail["line_num"] = 1
    detail["sku"] = req.form["sku"]
    detail["quantity"] = req.form["quantity"]
    detail["units_to_ship"] = detail["quantity"]
    detail["unit_price"] = req.form["price"]

    return detail
"""