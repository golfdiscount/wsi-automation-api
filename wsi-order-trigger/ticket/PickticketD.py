"""
Pick ticket detail which is generally a "line item" and contains
basic information for each product a customer ordered
"""
# noinspection SpellCheckingInspection
class PickticketD:
    _indices = {
        "pick_ticket_num": 2,
        "line_num": 3,
        "sku": 5,
        "quantity": 10,
        "units_to_ship": 11,
        "unit_price": 14
    }

    def __init__(self, record):
        """
        Instantiates a pick ticket detail object

        @type record: A pandas Series
        @param record: A record containing pick ticket detail information
        @raise ValueError: The record is not a pick ticket detail
        """
        self._info = {}

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

    def __repr__(self):
        """
        String representation of the object for internal use

        @return: A string detailing the pick ticket detail
        """
        return str(self._info)
