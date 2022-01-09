import datetime
import azure.functions as func
import json
import mysql.connector as sql
import os

def main(req: func.HttpRequest) -> func.HttpResponse:
    """Queries the database for the specified order number"""
    order_num = req.route_params.get('order_num')
    order_num = order_num.replace(';', '')
    order_num = order_num.replace('"', '')

    db: sql.MySQLConnection = sql.connect(
        user = os.environ['db_user'],
        password = os.environ['db_pass'],
        host = os.environ['db_host'],
        database = os.environ['db_database']
    )

    cursor = db.cursor(dictionary=True)

    qry = f"""
    SELECT wsi_order.order_num AS "order_num",
        wsi_order.`order_date`,
        wsi_order.ship_method,
        c.sold_to_name,
        c.sold_to_address,
        c.sold_to_city,
        c.sold_to_state,
        c.sold_to_country,
        c.sold_to_zip,
        r.ship_to_name,
        r.ship_to_address,
        r.ship_to_city,
        r.ship_to_state,
        r.ship_to_country,
        r.ship_to_zip,
        line_item.line_num,
        product.sku,
        line_item.quantity,
        product.unit_price
    FROM wsi_order
    JOIN customer AS c ON c.customer_id = wsi_order.sold_to
    JOIN recipient AS r ON r.recipient_id = wsi_order.ship_to
    JOIN line_item ON line_item.pick_ticket_num = wsi_order.pick_ticket_num
    JOIN product ON product.sku = line_item.sku
    WHERE order_num = "{order_num}";
    """

    cursor.execute(qry)
    json_res: dict = format_response(cursor)

    if len(json_res['products']) == 0:
        return func.HttpResponse(json.dumps({}), status_code=404, mimetype='application/json')

    return func.HttpResponse(json.dumps(json_res), mimetype='application/json')

def format_response(cursor) -> dict:
    """Formats response from database to the order model"""
    response = {
        'products': []
    }

    for row in cursor:
        orderDate: datetime.date = row['order_date']
        response['orderNum'] = row['order_num']
        response['orderDate'] = orderDate.strftime('%m-%d-%Y')
        response['shippingMethod'] = row['ship_method']
        response['customer'] = {
            'name': row['sold_to_name'],
            'address': row['sold_to_address'],
            'city': row['sold_to_city'],
            'state': row['sold_to_state'],
            'country': row['sold_to_country'],
            'zip': row['sold_to_zip']
        }
        response['recipient'] = {
            'name': row['ship_to_name'],
            'address': row['ship_to_address'],
            'city': row['ship_to_city'],
            'state': row['ship_to_state'],
            'country': row['ship_to_country'],
            'zip': row['ship_to_zip']
        }

        response['products'].append({
            'sku': row['sku'],
            'quantity': row['quantity'],
            'price': row['unit_price']
        })

    return response