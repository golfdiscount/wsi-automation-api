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
  "orderNum": "Order number",
  "orderDate": "Date of the order",
  "shippingMethod": "FDXH or FXES or FX2D or FXSO",
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
      "sku": "SKU number of product",
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
POSTing to the `/order` route creates an order. To create an order, submit an order
model as JSON in the request body. All attributes must be filled to create an order.

After the order is POSTed and inserted into the database, it will immediately be 
queued to be uploaded to WSI's filesystem. 

The API will return a response with the order information returned in CSV format
adhering to WSI's order format.