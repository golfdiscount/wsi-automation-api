# Pickticket
This repository contains a python module that holds the
implementation necessary to parse WSI orders from a
pick ticket containing a series of orders

## Overview of the Pickticket module
The pickticket module consists of three objects:
1. A Pickticket object
2. A PickticketH object
3. A PickticketD object

All together, these encapsulate a WSI order with one or many
SKU items listed on it

## The Pickticket Object

A Pickticket object represents the entirety of a WSI pick ticket file

The object can be instantiated by passing in a file name

Once instantiated, call `parse_orders()` to work through the file
and assign pick ticket details to their appropriate headers
```python
# Create a pickticket object
pickticket = pickticket(file_name)

# Parse through the file and look for headers and details
pickticket.parse_orders()
```

Orders for this object are contained in a dict as such:
```python
{
  pick_ticket_num: {
    "header": PickticketH,
    "details": {
      line_num: PickticketD,
      line_num: PickticketD
    }
  }
}
```
A pick ticket number is associated with a list which contains a header object and
a list of pick ticket details

## The PickticketH Object
**YOU WILL NEVER HAVE TO TOUCH THE SOURCE CODE UNLESS IMPLEMENTATION DETAILS SUCH AS INDICES CHANGE**

The PickticketH object represents a pick ticket header which contains information about the order such as:
- Customer information
- Recipient information
- Shipping information

These indices are used to get important information:
- pick_ticket_num - 2
- order_num - 3
- order_date - 5
- sold_to_name - 12
- sold_to_address - 13
- sold_to_city - 14
- sold_to_state - 15
- sold_to_country - 16
- sold_to_zip - 17
- ship_to_name - 19
- ship_to_address - 20
- ship_to_city - 21
- ship_to_state - 22
- ship_to_country - 23
- ship_to_zip - 24

The `get_info()` method will return information containing all fields relevant to the header as a dict

The naming convention of fields is consistent to what is shown above

## The PickticketD Object
**YOU WILL NEVER HAVE TO TOUCH THE SOURCE CODE UNLESS IMPLEMENTATION DETAILS SUCH AS INDICES CHANGE**

The PickticketD object represents a pick ticket detail object containing information about line items and their products

These indices are used to get important information:
- pick_ticket_num - 2
- line_num - 3
- sku - 5
- quantity - 10
- units_to_ship - 11
- unit_price - 14

The `get_info()` method will return information about a pick ticket detail as a dict

The naming convention of keys for the dict are consistent to what is shown above

There is one additional key, `sku_name` which is the name of the SKU pulled from ShipStation
