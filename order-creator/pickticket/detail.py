"""
Pick ticket detail which is generally a "line item" and contains
basic information for each product a customer ordered
"""
import pandas


class Detail:
    _indices = {
        "pick_ticket_num": 2,
        "line_num": 3,
        "sku": 5,
        "quantity": 10,
        "units_to_ship": 11,
        "unit_price": 14
    }

    def __init__(self):
        """Instantiates a pick ticket detail object"""
        self._info = {}

    def read_dict(self, record: dict):
        """
        Populates detail data using a dict

        @type record: dict
        @param record: Detail data including order number, product number, sku name, etc.
        """

        if type(record) != dict:
            raise TypeError("The passed in record must be a dict")

        for key in record:
            if key not in self._indices.keys():
                raise KeyError(f"Error: {key} is not a valid key")

        self._info = record

    def read_csv(self, record: pandas.Series):
        """
        Populates detail data using a CSV record

        @type record: pandas dataseries
        @param record: CSV record containing pick ticket details
        @return:
        """
        if record[0] != "PTD":
            raise ValueError("This passed record was not a pick ticket detail")
        for key in self._indices:
            self._info[key] = record[self._indices[key]]

    def get_pick_details(self):
        """
        Gets the pick ticket number and line number

        @rtype: dict
        @return: The pick ticket number and line number of the detail
        """
        return self._info

    def get_sku(self):
        """
        Returns the sku of this detail item

        @rtype: String
        @return: The sku for this detail item
        """
        return self._info["sku"]

    def set_sku_name(self, name):
        """
        Sets the sku name for this detail item

        @type name: String
        @param name: Name of the item for this detail object
        """
        self._info["sku_name"] = name

    def __str__(self):
        detail = [""]*27

        detail[0] = "PTD"
        detail[1] = "I"
        detail[4] = "A"
        detail[17] = "HN"
        detail[18] = "PGD"

        for index in self._indices:
            detail[self._indices[index]] = self._info[index]

        return ",".join([str(entry) for entry in detail])
