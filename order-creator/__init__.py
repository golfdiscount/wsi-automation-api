import logging
import tempfile
import azure.functions as func
from .pickticket.pickticket import Ticket


def main(req: func.HttpRequest) -> func.HttpResponse:
    logging.info('Python HTTP trigger function processed a request.')

    try:
        header = createHeader(req)
        detail = createDetail(req)
    except Exception as e:
        logging.warning(f"There was an error creating the ticket: {e}")
        return func.HttpResponse(f"There was an error creating the ticket\n{e}", status_code=500)

    ticket = Ticket()
    ticket.create_ticket(header, detail)

    temp_file_path = tempfile.gettempdir()
    temp = tempfile.NamedTemporaryFile()
    temp.write(bytes(str(ticket), "utf-8"))
        
    return func.HttpResponse(bytes(str(ticket), "utf-8"), status_code=200)

def createHeader(req: func.HttpRequest) -> dict:
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
