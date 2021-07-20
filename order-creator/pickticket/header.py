"""
A pick ticket header representing information about an order such as:

Order date
Customer information
Recipient information

Functions:
get_header() -> dict
get_cus_info() -> dict
get_rec_info(0 -> dict
"""
import pandas


class Header:
    # These indices are used for reading from a CSV file
    _indices = {
        "pick_ticket_num": 2,
        "order_num": 3,
        "order_date": 5,
        "sold_to_name": 12,
        "sold_to_address": 13,
        "sold_to_city": 14,
        "sold_to_state": 15,
        "sold_to_country": 16,
        "sold_to_zip": 17,
        "ship_to_name": 19,
        "ship_to_address": 20,
        "ship_to_city": 21,
        "ship_to_state": 22,
        "ship_to_country": 23,
        "ship_to_zip": 24,
        "ship_method": 32
    }

    def __init__(self):
        """Instantiate a pick ticket header with empty data"""
        self._info = {}

    def read_dict(self, record: dict):
        """
        Populates header data using a dict

        @type record: dict
        @param record: Header data including order number, shipping info, etc.
        @raise TypeError: Record passed in is not of type dict
        @raise: KeyError: A key in the passed in record is named incorrectly
        """
        if type(record) != dict:
            raise TypeError("The passed record must be a dict")

        for key in record:
            if key not in self._indices.keys():
                raise KeyError(f"Error: {key} is not a valid key")

        self._info = record
        self._norm_sold_add()
        self._norm_ship_add()

    def _norm_sold_add(self):
        """Wraps customer's address in quotes to avoid extraneous commas"""

        self._info["sold_to_name"] = f'"{self._info["sold_to_name"]}"'
        self._info["sold_to_address"] = f'"{self._info["sold_to_address"]}"'
        self._info["sold_to_city"] = f'"{self._info["sold_to_city"]}"'

    def _norm_ship_add(self):
        """Wraps customer's address in quotes to avoid extraneous commas"""

        self._info["ship_to_name"] = f'"{self._info["ship_to_name"]}"'
        self._info["ship_to_address"] = f'"{self._info["ship_to_address"]}"'
        self._info["ship_to_city"] = f'"{self._info["ship_to_city"]}"'

    def read_csv(self, record: pandas.Series):
        """
        Populates header data using a CSV record

        @type record: pandas series
        @param record: CSV record containing header details
        """
        if record[0] != "PTH":
            raise ValueError("This passed record was not a pick ticket header")

        for key in self._indices:
            self._info[key] = record[self._indices[key]]

    def get_header(self):
        """
        Gets the info about a pick ticket header

        @rtype: dict
        @return: The header record
        """
        return self._info

    def get_pick_num(self):
        """
        Gets the pick ticket number for this header

        @rtype: String
        @return: Pick ticket number
        """
        return self._info["pick_ticket_num"]

    def get_order_info(self):
        """
        Gets the information about the order

        @rtype: dict
        @return: Information about the order number and date
        """
        return {
            "pick_ticket_num": self._info["pick_ticket_num"],
            "order_num": self._info["order_num"],
            "order_date": self._info["order_date"],
            "ship_method": self._info["ship_method"]
        }

    def get_cus_info(self):
        """
        Gets the customer information for this order

        @rtype: dict
        @return: Name and address of the customer
        """
        return {
            "sold_to_name": self._info["sold_to_name"],
            "sold_to_address": self._info["sold_to_address"],
            "sold_to_city": self._info["sold_to_city"],
            "sold_to_state": self._info["sold_to_state"],
            "sold_to_country": self._info["sold_to_country"],
            "sold_to_zip": self._info["sold_to_zip"]
        }

    def get_rec_info(self):
        """
        Gets the recipient information for this order

        @rtype: dict
        @return: Name and address of the recipient
        """
        return {
            "ship_to_name": self._info["ship_to_name"],
            "ship_to_address": self._info["ship_to_address"],
            "ship_to_city": self._info["ship_to_city"],
            "ship_to_state": self._info["ship_to_state"],
            "ship_to_country": self._info["ship_to_country"],
            "ship_to_zip": self._info["ship_to_zip"]
        }

    def __str__(self):
        header = [""]*62
        header[0] = 'PTH'
        header[1] = 'I'
        header[4] = 'C'

        header[9] = "75"

        header[35] = 'PGD'
        header[37] = 'HN'
        header[38] = 'PGD'
        header[39] = 'PP'

        header[45] = 'Y'
        header[49] = "PT"

        for index in self._indices:
            header[self._indices[index]] = self._info[index]

        return ",".join([str(entry) for entry in header])
