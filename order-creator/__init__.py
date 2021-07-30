import logging
import azure.functions as func
from .pickticket.pickticket import Ticket


def main(req: func.HttpRequest) -> func.HttpResponse:
    logging.info('Python HTTP trigger function processed a request.')

    try:
        header = createHeader(req)
        detail = createDetail(req)
    except Exception as e:
        return func.HttpResponse(f"There was an error creating the ticket\n{e}", status_code=500)

    ticket = Ticket()
    ticket.create_ticket(header, detail)

    with open(f"{header['order_num']}.csv", 'w+') as f:
        f.writelines(str(ticket))
        bytes(f)
        return func.HttpResponse(f.read(), mimetype='text/plain', status_code=200)

def createHeader(req: func.HttpResponse) -> dict:
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
    if req.form["storeNum"] == 1:
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

def createDetail(req: func.HttpResponse) -> dict:
    detail = {}

    detail["pick_ticket_num"] = f"C{req.form['order_num']}"
    detail["line_num"] = 1
    detail["sku"] = req.form["sku"]
    detail["quantity"] = req.form["quantity"]
    detail["units_to_ship"] = detail["quantity"]
    detail["unit_price"] = req.form["price"]

    return detail