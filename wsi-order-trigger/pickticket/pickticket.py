"""Creates a pick ticket object holding order information"""
import pandas as pd
from .header import Header
from .detail import Detail


class Ticket:
    def __init__(self):
        """Initiate a pick ticket object"""
        self._orders = {}

    def create_ticket(self, header_data, detail_data):
        header = Header()
        header.read_dict(header_data)
        detail = Detail()
        detail.read_dict(detail_data)

        pick_num = header.get_pick_num()
        line_num = detail.get_pick_details()["line_num"]

        self._orders[pick_num] = {"header": header}
        self._orders[pick_num]["details"] = {line_num: detail}

    def read_csv(self, file_path):
        """
        Reads a CSV and creates a ticket from it
        @param file_path: Path to the CSV file
        @return:
        """
        try:
            tickets = pd.read_csv(file_path, header=None)
            self._parse_orders(tickets)
        except FileNotFoundError as e:
            raise e
        except Exception as e:
            raise e

    def _parse_orders(self, tickets):
        """
        Parses a set of orders from a file

        @type tickets: A pandas DataFrame
        @param tickets: Table containing pick ticket headers and details
        """
        for i, row in tickets.iterrows():
            if row[0] == "PTH":
                self._create_header(row)
            else:
                self._create_detail(row)

    def _create_header(self, record):
        """
        Creates and adds a pick ticket header to the orders set

        @type: A pandas Series
        @param record: Pick ticket header record
        """
        header = Header()
        header.read_csv(record)
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
        detail = Detail()
        detail.read_csv(record)
        pick_num = detail.get_pick_details()["pick_ticket_num"]
        line_num = detail.get_pick_details()["line_num"]

        if pick_num not in self._orders:
            self._orders[pick_num] = {"details": {line_num: detail}}
        elif "details" not in self._orders[pick_num]:
            self._orders[pick_num]["details"] = {line_num: detail}
        else:
            self._orders[pick_num]["details"][line_num] = detail

    def get_orders(self):
        """
        Gets the set of orders for this pick ticket

        @rtype: dict
        @return: Set of orders and their associated headers and details
        """
        return self._orders

    def __iter__(self):
        """Defines the iterable behavior of this object"""
        return iter(self._orders)

    def __str__(self):
        result = ""

        for pick_num in self._orders:
            result += str(self._orders[pick_num]["header"]) + "\n"

            for line in self._orders[pick_num]["details"]:
                result += str(self._orders[pick_num]["details"][line]) + "\n"

        return result
