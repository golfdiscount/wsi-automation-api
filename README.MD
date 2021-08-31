# Overview
This is a implementation of an API used to create and upload orders to the WSI database and SFTP new orders to WSI to be picked and pulled.

The API runs on an Azure Functions App using multiple endpoints which will be described below.

# API Requirements

## Domain
All endpoints lie at the domain https://wsi.azurewebsites.net/api

## Authentication
There are currently no authentication protocols but that may change over time.

# Endpoints

There are two endpoints encapsulating order creation and order importing.

## /createOrder
To create an order, a variety of different attributes must be passed in. At its core, the `createOrder` endpoint relies on implementation supplied by the [`Pickticket Package`](https://github.com/pgd-harmeet/Pickticket) to create a pick ticket object.

After the pick ticket is created, the `/importOrder` endpoint is immediately called to facilitate uploading the order to the database and to the WSI server.

### Request

In order to start creating a pick ticket, you must supply the following attributes at a minimum:

```JSON
{
	"order_num": "Order number",
	"order_date": "Order date",
	"sold_to_name": "Customer's name",
	"sold_to_address": "Customer's address",
	"sold_to_city": "Customer's city",
	"sold_to_state": "Customer's state",
	"sold_to_country": "Customer's country",
	"sold_to_zip": "Customer's ZIP code",
	"sku": "SKU number of the product ordered",
	"quantity": "Number of items ordered",
	"unit_price": "Price of a single unit"
}
```

The name and address fields shown above are merely the billing address. With the request above, the shipping address is the same as the billing address.

**In the case the shipping address is different, add the following attributes to the request above**

```JSON
{
	"ship_to_name": "Recipient's name",
	"ship_to_address": "Recipient's address",
	"ship_to_city": "Recipient's city",
	"ship_to_state": "Recipient's state",
	"ship_to_country": "Recipient's country",
	"ship_to_zip": "Recipient's ZIP code"
}
```

### Response
The response returned from the `/createOrder` endpoint is a bytes array containing the order information encoded in `utf-8`. Use any decoding tool to convert from a bytes array to String format.

## /importOrder

`/importOrder` is responsible for taking an order and:
1. Inserting into the WSI MySQL DB
2. Uploading the file via SFTP to the WSI server to be picked and pulled

### Request
To submit a file to be uploaded, you have two choices:
1. A file upload via form data with a key of `file` and the file object attached
2. A bytes array of the pick ticket

### Response
When the upload completes successfully, you will get a 200 response from the server letting you know that orders have been successfully uploaded.

For all other errors, you will get a 500 error as well as a stack trace attached to the response object. Additionally, the complete log can be found on the Azure portal by navigating to the function app, and taking a look at the logs from there.

# Questions or concerns?
If you have any questions or concerns, please feel free to contact the developer and maintainer of this API, Harmeet Singh, at harmeet@golfdiscount.com.
