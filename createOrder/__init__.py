"""
Creates a single order in the database and adds it to the blob storage container
"""

import azure.functions as func
import logging

import os

from azure.storage.blob import BlobClient
from datetime import datetime
from io import StringIO
from uuid import uuid4
from wsi.pickticket import Pickticket
from wsi.order import Order

def main(req: func.HttpRequest) -> func.HttpResponse:
    """Entry point of HTTP request

    Args:
        req: azure.functions.HttpRequest with order information in JSON body

    Return:
        azure.functions.HttpResponse with status code indicating if insertion was successful
    """

    try:
        if req.headers['content-type'] != 'application/json':
            ticket = Pickticket()
            ticket.read_csv(StringIO(req.get_body().decode('utf-8')))
        else:
            order = Order()
            order.from_dict(req.get_json())
    except KeyError as e:
        logging.error(f'Missing key {e} from request object')
        return func.HttpResponse('Please make sure to include all attributes for the order model and ensure correct headers are present', status_code=400)
    except ValueError as e:
        return func.HttpResponse(str(e), status_code=400)

    return func.HttpResponse('Order submitted')

def export_order(order: Order) -> None:
    """Exports an order to a blob container to be SFTP to WSI

    This should be used to export a singular order

    Args:
        order: Order to be exported
    """
    blob = BlobClient.from_connection_string(os.environ['AzureWebJobsStorage'],
        container_name='wsi-orders',
        blob_name=str(uuid4()))
    blob.upload_blob(order.to_csv())

def export_ticket(ticket: Pickticket) -> None:
    """Exports a pick ticket to a blob container to be SFTP to WSI

    This should be used to export a series of orders

    Args:
        ticket: Pickticket with orders to be exported
    """

    blob = BlobClient.from_connection_string(os.environ['AzureWebJobsStorage'],
        container_name='wsi-orders',
        blob_name=str(uuid4()))
    blob.upload_blob(ticket.to_csv())