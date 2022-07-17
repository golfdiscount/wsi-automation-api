"""
This module represents a singular WSI order.
An order can be read from either a CSV file or JSON format containing information pertaining to the order model
"""
import datetime
import pandas


class Order:
    _header_indices = {
        'orderNumber': 3,
        'orderDate': 5,
        'customer': {
            'name': 12,
            'address': 13,
            'city': 14,
            'state': 15,
            'country': 16,
            'zip': 17
        },
        'recipient': {
            'name': 19,
            'address': 20,
            'city': 21,
            'state': 22,
            'country': 23,
            'zip': 24
        },
        'shippingMethod': 32
    }

    _detail_indices = {
        'sku': 5,
        'quantity': 10,
        'price': 14
    }

    def __init__(self) -> None:
        """Creates a an order object"""
        self.order_num = None
        self._order = {}

    def from_dict(self, order_info: dict):
        """Validates order information and saves it to the order instance
        if all keys are present

        Args:
            order_info: dict containing order information
        Raises:
            KeyError: A required key is missing from the dict
        """
        keys = ['orderNumber', 'orderDate', 'shippingMethod', 'customer', 'recipient', 'products']
        for key in keys:
            if key not in order_info:
                raise KeyError(f'Missing key {key} in order')

        address_keys = ['name', 'address', 'city', 'state', 'country', 'zip']
        for key in address_keys:
            if key not in order_info['customer'] or key not in order_info['recipient']:
                raise KeyError(f'Customer or recipient address missing key {key}')

        product_keys = ['sku', 'quantity', 'price']
        for product in order_info['products']:
            for key in product_keys:
                if key not in product:
                    raise KeyError(f'A product is missing key {key}')

        self.order_num = order_info['orderNumber']
        self._order = order_info

    def from_csv(self, csv):
        """Populates order information through CSV records

        Args:
            csv: filepath or a buffer
        """
        order = pandas.read_csv(csv, header=None)
        self.from_df(order)

    def from_df(self, order: pandas.DataFrame):
        """Populate order information from a DataFrame. This is primarily used when constructing picktickets.

        Args:
            order: DataFrame containing order information
        """
        for i, record in order.iterrows():
            if record[0] == 'PTH':
                self._parse_header_csv(record)
            else:
                self._parse_detail_csv(record)

    def _parse_header_csv(self, record: pandas.Series):
        """Parses a header record

        Args:
            record: pandas Series containing order header
        """
        for field in self._header_indices:
            if field == 'customer' or field == 'recipient':  # Iterate over address fields
                if field not in self._order:
                    self._order[field] = {}
                for address_field in self._header_indices[field]:
                    self._order[field][address_field] = record[self._header_indices[field][address_field]]
            else:
                self._order[field] = record[self._header_indices[field]]

    def _parse_detail_csv(self, record: pandas.Series):
        """Parses a detail record

        Args:
            record: pandas Series containing an order detail record
        """
        detail = {}

        for field in self._detail_indices:
            detail[field] = record[self._detail_indices[field]]

        if 'products' not in self._order:
            self._order['products'] = [detail]
        else:
            self._order['products'].append(detail)

    def to_csv(self) -> str:
        """Generates the CSV record for this order"""
        csv = ''
        csv += self._generate_header_csv() + '\n' + self._generate_detail_csvs()
        return csv

    def _generate_header_csv(self) -> str:
        """Generates a WSI header record in a CSV format

        Raises:
            RuntimeError: A CSV was attempted to be generated before order information is populated
        Returns:
            CSV record in WSI format with header information
        """
        if not self._order:
            raise RuntimeError('The order has not been populated yet')

        header = [''] * 62  # Header has 62 fields
        header[0] = 'PTH'
        header[1] = 'I'
        header[2] = 'C' + str(self._order['orderNumber'])
        header[4] = 'C'
        header[9] = '75'
        header[35] = 'PGD'
        header[37] = 'HN'
        header[38] = 'PGD'
        header[39] = 'PP'
        header[45] = 'Y'
        header[49] = 'PT'

        for index in self._header_indices:
            if index == 'orderDate':
                try:  # Try to form date out of ISO format
                    order_date = datetime.date.fromisoformat(self._order[index])
                    header[self._header_indices[index]] = order_date.strftime('%m/%d/%Y')
                except ValueError:  # Date is not in ISO format, assumes date is in MM/DD/YYYY format
                    header[self._header_indices[index]] = self._order[index]

            elif index == 'customer' or index == 'recipient':  # Iterate over address dicts
                for address_field in self._header_indices[index]:
                    if address_field == 'name' or address_field == 'address':
                        # Specific filtering for name and address fields is used to be consistent with formatting
                        # from Artem's team which wraps those fields in quotes
                        header[self._header_indices[index][address_field]] = f'"{self._order[index][address_field]}"'
                    else:
                        header[self._header_indices[index][address_field]] = self._order[index][address_field]
            else:
                header[self._header_indices[index]] = self._order[index]

        return ','.join([str(entry) for entry in header])

    def _generate_detail_csvs(self) -> str:
        """Generates all WSI detail record in a CSV format, one for
        each product in the order

        Returns:
            A series of CSV detail records, one for each product. Each detail record is separated by a newline character
        """
        if not self._order:
            raise RuntimeError('The order has not been populated yet')

        csvs = ''
        line_num = 0
        for product in self._order['products']:
            line_num += 1
            detail = [''] * 27  # detail has 27 fields
            detail[0] = 'PTD'
            detail[2] = 'C' + str(self._order['orderNumber'])
            detail[3] = str(line_num)
            detail[1] = 'I'
            detail[4] = 'A'
            detail[11] = str(int(product['quantity']))
            detail[17] = 'HN'
            detail[18] = 'PGD'

            for index in self._detail_indices:
                if index == 'quantity':  # Quantity field so truncate decimals that may be present
                    detail[self._detail_indices[index]] = str(int(product[index]))
                else:
                    detail[self._detail_indices[index]] = product[index]

            csvs += ','.join([str(entry) for entry in detail]) + '\n'
        return csvs

    def to_dict(self) -> dict:
        """Gets the dict of this order"""
        return self._order

    def __repr__(self) -> str:
        """Representation of order object for debugging"""
        return f'<Order {self._order["orderNumber"]}>'
