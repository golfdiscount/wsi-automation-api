"""
Creates a pick ticket object holding order information

Functions:
"""
import pandas as pd
from .PickticketH import PickticketH as pth
from .PickticketD import PickticketD as ptd


# noinspection SpellCheckingInspection
class Pickticket:
    def __init__(self, file_path):
        """
        Initiate a pick ticket object

        @param file_path: Path to a CSV file containing pick tickets
        """
        self._orders = {}
        try:
            ticket_data = pd.read_csv(file_path, header=None)
            self._parse_orders(ticket_data)
        except FileNotFoundError as e:
            raise e
        except Exception as e:
            raise e

    def _parse_orders(self, tickets):
        """
        Parse a set of orders

        @type tickets: A pandas DataFrame
        @param tickets: Table containing pick ticket headers and details
        """
        for i, row in tickets.iterrows():
            if row[0] == "PTH":
                self._create_header(row)
            else:
                self._create_detail(row)

    def get_orders(self):
        """
        Gets the set of orders for this pick ticket
        """
        return self._orders

    def _create_header(self, record):
        """
        Creates and adds a pick ticket header to the orders set

        @type: A pandas Series
        @param record: Pick ticket header record
        """
        header = pth(record)
        pick_num = header.get_pick_num()

        if pick_num not in self._orders:
            self._orders[pick_num] = {"header": header}
        else:
            self._orders[pick_num]["header"] = header

    def _create_detail(self, record):
        """
        Creates and addas a pick ticket header to the orders set

        @type: A pandas Series
        @param record: Pick ticket detail record
        """
        detail = ptd(record)
        pick_num = detail.get_pick_details()["pick_ticket_num"]
        line_num = detail.get_pick_details()["line_num"]

        if pick_num not in self._orders:
            self._orders[pick_num] = {"details": {line_num: detail}}
        elif "details" not in self._orders[pick_num]:
            self._orders[pick_num]["details"] = {line_num: detail}
        else:
            self._orders[pick_num]["details"][line_num] = detail

    def print_ticket(self):
        """
        Prints out the representation of a pick ticket

        THIS IS ONLY FOR DIAGNOSTIC USE
        """
        for order in self._orders:
            print(self._orders[order]["header"])
            for detail in self._orders[order]["details"]:
                print(f"{self._orders[order]['details'][detail]}")

    def __iter__(self):
        return iter(self._orders)
