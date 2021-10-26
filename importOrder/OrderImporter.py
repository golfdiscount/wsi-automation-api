import os
import requests

from io import StringIO
from pickticket.pickticket import Ticket

class OrderImporter:
    _skus = {}

    def __init__(self):
        """
        Initializes an order importer

        @raise AssertionError: The health check to the ShipStation API returned a status code != 200
        """
        self.session = requests.Session()
        self.session.headers.update({"Authorization": os.environ["SS_CREDS"]})

        # Do a health check request
        res = self.session.get("https://ssapi.shipstation.com/stores")
        assert res.status_code == 200, f"There was an issue connection to ShipStation: {res.text}"

    def process_fo(self, file):
        self.ticket = Ticket(file)
        self._upload_to_api()

    def process_bytes(self, file_bytes: bytes):
        self.ticket = Ticket(StringIO(file_bytes))

    def _upload_to_db(self):
        """
        Process a list of orders and inserts them into the database
        """
        orders =  self.ticket.get_orders()
        for order in orders:
            header = orders[order]["header"]

            res = requests.post(os.environ.get("shipstation_url") + "/queueCustomerNote", params={"orderNumber": header.get_pick_num()[1:]})
            assert res.status_code == 200, f"There was an issue queuing order {header.get_pick_num()}"

    def _get_sku_name(self, sku: str) -> str:
        """
        Gets the name of a SKU

        @param sku: The SKU of which to get a name for
        @return: The matching SKU name or None if cannot be found
        """
        if sku in self._skus:
            return self._skus[sku]

        res = self.session.get("https://ssapi.shipstation.com/products", {"sku": sku})

        for product in res["products"]:
            if product["sku"] == sku:
                self._skus[sku] = product["name"]
                return product["name"]

        return None