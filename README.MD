# Overview
The WSI API handles order functionality pertaining to WSI. This includes creating orders, importing
orders to the database, and initiating SFTP to WSI's filesystem.

# API Requirements
All endpoints are located at https://wsi.azurewebsites.net/api.

# Models
These models are used to represent pieces of information in the WSI API. Models
are used in responses from the API and requests to the API.

## Order
```json
{
  "order_num": "Order number",
  "order_date": "Order date in mm-dd-yyyy format",
  "shipping_method": "FDXH or FXES or FX2D or FXSO",
  "customer": {
    "name": "Customer first and last name",
    "address": "Customer address, including lines apt, suite, unit number, etc.",
    "city": "Customer city",
    "state": "Customer state",
    "zip": "Customer zip code",
    "country": "Customer's country"
  },
  "recipient": {
    "name": "Recipient first and last name",
    "address": "Recipient address, including lines apt, suite, unit number, etc.",
    "city": "Recipient city",
    "state": "Recipient state",
    "zip": "Recipient zip code",
    "country": "Recipient's country"
  },
  "products": [
    {
      "sku": "SKU number of product wrapped in quotes, should be a string not a number",
      "quantity": "Quantity of product ordered",
      "price": "Price of product as a decimal without a $ at the front"
    }
  ]
}
```

# Endpoints

## GET

### /order/{orderNum}
Submitting a GET request to the `/order/{orderNum}` will return the order
associatd with the order number in form of an order model in JSON format.

## POST

### /order
POSTing to the `/order` route creates an order. To create a single order, submit order information following the order model as JSON in the request body. All attributes must be filled to create an order. When submitting JSON data, ensure that
you include the `content-type` header of `application-json` so that request is processed correctly.

To upload multiple orders, CSV contents must be present in the request body. The CSV contents must follow WSI's pick ticket header & detail format.

After the order is POSTed and inserted into the database, it will be
queued to be uploaded to WSI's filesystem. An Azure blob (binary large object) trigger will process the file and SFTP it to WSI's filesystem.