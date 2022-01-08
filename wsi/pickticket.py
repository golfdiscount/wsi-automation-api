"""
This module represents a WSI pickticket which is a collection of WSI orders. A Pickticket
object supports creating a pickticket from a CSV or making a CSV from a pickticket. To populate a
pickticket, you can use:
- read_csv
- add_order
"""
import pandas

from wsi.order import Order


class Pickticket:
    """Object that holds a set of orders"""
    def __init__(self):
        self._orders = set()

    def read_csv(self, csv) -> None:
        """Reads a CSV and generates a collection of orders out of it

        Args:
            csv: filepath or a buffer with CSV contents
        """
        orders = pandas.read_csv(csv, header=None)
        grouped_orders = orders.groupby(by=2)  # Group by the order key which is in a consistent location

        for name, group in grouped_orders:
            order = Order()
            order.from_df(group)
            self._orders.add(order)

    def add_order(self, wsi_order: Order) -> None:
        self._orders.add(wsi_order)

    def size(self) -> int:
        return len(self._orders)

    def csv(self) -> str:
        result = ''

        for order in self._orders:
            result += order.csv()

        return result
