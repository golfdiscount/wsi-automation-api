# Overview
The `order-creator` endpoint facilitates the creation of a WSI pick ticket so
that it can be inserted into the database.

# Submitting a Request to Create a Ticket
## Basic Request Body
To create a pick ticket, the following attributes must be present in the
request body:
```JSON
{
    "order_num": "Order number",
    "order_date": "Date of the order",
    "sold_to_name": "Customer's name",
    "sold_to_address": "Customer's address",
    "sold_to_city": "Customer's city",
    "sold_to_state": "Customer's state",
    "sold_to_country": "Customer's country",
    "sold_to_zip": "Customer's zip code",
    "line_num": "Line number of the pick ticket's detail, usually is 1",
    "sku": "Product's SKU number",
    "quantity": "The number of SKU units ordered",
    "units_price": "Price of each SKU unit"
}
```
## Additional Attributes
There are some additional attributes available as well but have some default
values in the API.

### Recipient Address
In the case that the recipient's address is different from the customer's
address, you can add the following attributes to reference the recipient's
address.

```JSON
"ship_to_name": "Recipient's name",
"ship_to_address": "Recipient's address",
"ship_to_city": "Recipient's city",
"ship_to_state": "Recipient's state",
"ship_to_country": "Recipient's country",
"ship_to_zip": "Recipient's zip code",
```
*These must be appended to the same request that contains the rest of the information.*

## Submitting a Ticket with Multiple Items
As of the current API definition, each order can only have one item attached to
it. This means that each pick ticket header will only have **one** detail
associated with it.
