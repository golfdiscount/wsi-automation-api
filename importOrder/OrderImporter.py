import datetime
import logging
import mysql.connector
import os
import paramiko as pm
import requests

from io import StringIO
from paramiko import AutoAddPolicy
from paramiko.sftp_client import SFTPClient
from pickticket.pickticket import Ticket
from . import wsi

class OrderImporter:
    _skus = {}

    def __init__(self):
        """Initializes an order importer and sets up any necessary connections"""
        self.session = requests.Session()
        self.session.headers.update({"Authorization": os.environ["SS_CREDS"]})

        self.cnx: mysql.connector.MySQLConnection = mysql.connector.connect(user=os.environ['db_user'],
                                password=os.environ['db_pass'],
                                host=os.environ['db_host'],
                                database=os.environ['db_database'])
        self.cursor: mysql.connector.MySQLConnection.cursor = self.cnx.cursor()

    def process_fo(self, file):
        """Turns a file object in a Pickticket object
        
        Args:
            file: An object with a .read() method with pickticket information
        """
        self._file = file
        self.ticket = Ticket(file)

    def process_bytes(self, file_bytes: bytes):
        """Turns a byte stream into a Pickticket object
        
        Args:
            file_bytes: A bytes object with pickticket information
        """
        self._file = StringIO(file_bytes)
        self.ticket = Ticket()
        self.ticket.read_csv(StringIO(file_bytes))

    def trigger_upload_flow(self):
        """Starts a workflow run of trigger a file upload
        
        1) Upload to database
        2) SFTP orders to WSI filesystem
        3) Queue orders to addCustomerNote queue
        """
        logging.info("Uploading to database...")
        self._upload_to_db()
        logging.info("Initating SFTP to WSI...")
        self._upload_sftp()
        logging.info("Queueing orders into the addCustomerNote queue")
        self._queue_customer_notes()

    def _upload_to_db(self):
        """Process a list of orders and inserts them into the database
        
        Raises:
            RuntimeError: Duplicate orders in the file or from a previous file
        """
        orders = self.ticket.get_orders()
        try:
            for order in orders:
                header = orders[order]["header"]
                logging.info(f"Uploading order {header.get_pick_num()}")
                self._upload_header(header)

                details = orders[order]["details"]
                for detail in details:
                    self._upload_detail(details[detail])

                logging.info("Committing data to dtatbase...")
                self.cnx.commit()
        except mysql.connector.IntegrityError:
            logging.error("Relational integrity error, there is most likely duplicate orders in this file or from previously inserted files")
            self.cnx.rollback()
            logging.error("Rolling back data...")
            raise RuntimeError("Relational integrity error, there is most likely duplicate orders in this file or from previously inserted files")
        finally:
            logging.info("Closing connection to database...")
            self.cnx.close()
    
    def _upload_header(self, header):
        """Inserts a header record into the database"""
        sold_to_id = wsi.add_cus(self.cursor, header.get_cus_info())
        ship_to_id = wsi.add_rec(self.cursor, header.get_rec_info())

        order_info = header.get_order_info()
        wsi.add_order(self.cursor, {
            "pick_ticket_num": order_info["pick_ticket_num"],
            "order_num": order_info["order_num"],
            "sold_to": sold_to_id,
            "ship_to": ship_to_id,
            "ship_method": order_info["ship_method"],
            "order_date": order_info["order_date"]
        })

    def _upload_detail(self, detail):
        """Inserts a detail record into the database"""
        sku = detail.get_sku()
        detail.set_sku_name(self._get_sku_name(sku))
        detail = detail.get_pick_details()
        # Add product
        wsi.add_product(self.cursor, {
            "sku": detail["sku"],
            "sku_name": detail["sku_name"],
            "unit_price": detail["unit_price"]
        })

        # Add line item
        wsi.add_lt(self.cursor, {
            "pick_ticket_num": detail["pick_ticket_num"],
            "line_num": detail["line_num"],
            "units_to_ship": detail["units_to_ship"],
            "sku": detail["sku"],
            "quantity": detail["quantity"]
        })

    def _upload_sftp(self):
        """Uploads the file of orders to WSI's filesystem"""
        now = datetime.datetime.now()
        date_string = now.strftime(f"%m_%d_%Y_%H_%M_%S")
        client = pm.SSHClient()
        client.set_missing_host_key_policy(AutoAddPolicy())

        logging.info("Connecting to WSI Server...")
        client.connect(os.environ['WSI_HOST'], username=os.environ['WSI_USER'], password=os.environ['WSI_PASS'])
        logging.info("Connection successful")

        self.file.seek(0)
        sftp = SFTPClient.from_transport(client.get_transport())
        logging.info(f"Uploading files to the {os.environ['target']} directory")
        sftp.putfo(self.file, f"/{os.environ['target']}/PT_WSI_{date_string}.csv", confirm=False)
        logging.info("SFTP finished successfully")

        client.close()

    def _queue_customer_notes(self):
        """Queues orders to have "Sent to WSI" note added"""
        orders =  self.ticket.get_orders()
        for order in orders:
            header = orders[order]["header"]

        requests.post(os.environ.get("shipstation_url") + "/queueCustomerNote", params={"orderNumber": header.get_pick_num()[1:]})

    def _get_sku_name(self, sku: str) -> str or None:
        """
        Gets the name of a SKU

        Args:
            sku (str): The SKU of which to get a name for
        
        Return:
            The matching SKU name or None if cannot be found
        """
        if sku in self._skus:
            return self._skus[sku]

        res = self.session.get("https://ssapi.shipstation.com/products", params={"sku": sku})

        for product in res["products"]:
            if product["sku"] == sku:
                self._skus[sku] = product["name"]
                return product["name"]

        return None