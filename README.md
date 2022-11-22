# Overview
This API is used to facilitate an order management system (OMS) for [WSI](https://www.wsinc.com/).
Basic REST API CRUD routes are provided in the form of HTTP triggers and additional process
automation is done through the use of blob, queue, and timer triggers.

## Technology
The runtime environment for this application is an
[Azure Functions App](https://learn.microsoft.com/en-us/azure/azure-functions/) using Functions
Runtime v4. All development is done on the .NET 6 framework.

# Configuration

## Developing locally
For a comprehensive guide on local development, please see Microsoft's documentation on
[Code and Test Azure Functions Locally](https://learn.microsoft.com/en-us/azure/azure-functions/functions-develop-local).
The key points are as follows:
- Download and install the [Azure Functions Core Tools](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools)
- Download [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite#install-azurite) to emulate a local storage account
- Store environment variables in `local.settings.json`

## Environment Variables
**The following environment variables are required.**

- AzureWebJobsStorage: The URI for the storage account the function app can use
- FUNCTIONS_WORKER_RUNTIME: Function worker language runtime, should be set to "dotnet"
- VAULT_URI: URI to an Azure KeyVault holding connection string, API keys, and other sensitive materials
- WSI_MASTER_SKU_URI: URI to a CSV file containing a master list of all SKUs that should be in WSI's inventory
- WSI_PO_DAILY_URI: URI to a CSV file containing current open POs from Eagle that need to be sent to WSI
- WSI_PO_MASTER_URI: URI to a CSV file containing all line items for all open POs for WSI


## KeyVault Secrets
**The following KeyVault secrets are required. Whoever is developing/debugging the application
must at a minimum have `GET` permissions for KeyVault secrets.**

- db-host: URI to a SQL Server database containing database tables to store WSI information
- dufferscorner-uri: URI to root of dufferscorner website
- magento-key: API function key to Magento API
- magento-uri: URI for magento API
- sendgrid-key: SendGrid API key
- shipstation-key:[ShipStation API key](https://www.shipstation.com/docs/api/requirements/#authentication)
- shipstation-secret: [ShipStation API Secret](https://www.shipstation.com/docs/api/requirements/#authentication)
- shipstation-uri: URI to ShipStation API
- wsi-pass: Password for WSI SFTP credentials
- wsi-uri: URI for WSI SFTP
- wsi-user: User for WSI SFTP credentials

# Triggers

## Blob Triggers

### SftpBlob
Triggers on the blob storage path `sftp/{name}` and initiates SFTP for the blob to WSI at the path
`Inbound/{name}`.

## HTTP Triggers

### Orders

#### Getting an order
You can get a singular order or a list of recent orders at the path `/api/orders/{orderNumber?}`.

##### Response Body
```json
{
    "pickTicketNumber": "Unique pickticket number",
    "orderNumber": "Original order number from Magento",
    "action": "Order action (typically I for insert)",
    "store": {
        "name": "Pro Golf Internet",
        "street": "13405 SE 30th St Suite 1A",
        "city": "Bellevue",
        "state": "WA",
        "country": "US",
        "zip": "98005",
        "storeNumber": 1
    },
    "customer": {
        "name": "Customer name",
        "street": "Customer street address",
        "city": "Customer city",
        "state": "Customer state",
        "country": "Customer country",
        "zip": "Customer zip code"
    },
    "recipient": {
        "name": "Recipient name",
        "street": "Recipient street address",
        "city": "Recipient city",
        "state": "Recipient state",
        "country": "Recipient country",
        "zip": "Recipient zip code"
    },
    "shippingMethod": {
        "code": "FDXH",
        "description": "FedEx Home Delivery",
        "created_at": "2022-08-04T23:47:04.59",
        "updated_at": "2022-08-04T23:47:04.59"
    },
    "lineItems": [
        {
            "pickticketNumber": "Unique pickticket number",
            "lineNumber": "Line number",
            "action": "Line action (typically I for insert)",
            "sku": "Product SKU",
            "units": "Quantity ordered",
            "unitsToShip": "Quantity authorized to ship",
            "created_at": "Date created",
            "updated_at": "Date last updated"
        }
    ],
    "orderDate": "Order date",
    "channel": "Channel number",
    "createdAt": "Date created",
    "updatedAt": "Date updated"
}
```

##### Expected Return Codes
| Code | Description |
| ---- | ----------- |
| 200 | An order was found and returned |
| 404 | An order was not found for the provided order number |

#### Creating an order
Triggers on the path `/api/orders` and creates a new WSI order in the database, generates a CSV
file for it, and sends it to WSI.

##### Content-Types
This route accepts two content types:
- application/json
- text/csv

`application/json` is used when submitting a singular order via a JSON body. This is typically
done via Postman or the order interface on the [Golf Discount Intranet](http://inet.golfdiscount.com).

`text/csv` is used when submitting a CSV of orders formatted to WSI's specification. This is
typically done when taking orders created by Magento and uploading them to this route.

##### Request Body
```json
{
    "orderNumber": "Order number from Magento",
    "orderDate": "Date of order in YYYY-MM-DD format",
    "store": 1,
    "shippingMethod": "FDXH",
    "customer": {
        "name": "Customer name",
        "street": "Customer street address",
        "city": "Customer city",
        "state": "Customer state",
        "country": "Customer country",
        "zip": "Customer zip code"
    },
    "recipient": {
        "name": "Recipient name",
        "street": "Recipient street address",
        "city": "Recipient city",
        "state": "Recipient state",
        "country": "Recipient country",
        "zip": "Recipient zip code"
    },
    "lineItems": [
        {
            "sku": "Product SKU",
            "units": "Quantity ordered",
            "lineNumber": 1
        }
    ]
}
```

##### Expected Return Codes
| Code | Description |
| ---- | ----------- |
| 202 | The order was successfully created |
| 400 | A bad request was submitted. This could either happen because of: JSON body formatting errors, a set of required attributes was not given, or a missing Content-Type header. |

### Purchase Orders
Triggers on the HTTP path `/api/pos/{poNumber}` and returns a JSON object representing PO information.

#### Expected Return Codes
| Code | Description |
| ---- | ----------- |
| 200 | An PO was found and returned |
| 404 | An PO was not found for the provided order number |

### Shipping Methods
Triggers on the HTTP path `shipping/{code:alpha?}` and returns a JSON object representing
shipping method information. If `code` is not specified, returns a listing of all shipping
methods.

#### Response Body
```json
{
    "code": "FDXH",
    "description": "FedEx Home Delivery",
    "created_at": "2022-08-04T23:47:04.59",
    "updated_at": "2022-08-04T23:47:04.59"
}
```

#### Expected Return Codes
| Code | Description |
| ---- | ----------- |
| 200 | A shipping method was found and returned |
| 404 | A shipping method was not found for the provided shipping code |

### Stores
Triggers on the HTTP path `stores/{id:int?}` and returns a JSON object representing
store information. if `id` is not specified, returns a listing of all stores.

#### Response Body
```json
[
    {
        "name": "Pro Golf Internet",
        "street": "13405 SE 30th St Suite 1A",
        "city": "Bellevue",
        "state": "WA",
        "country": "US",
        "zip": "98005",
        "storeNumber": 1
    }
]
```

#### Expected Return Codes
| Code | Description |
| ---- | ----------- |
| 200 | A store was found and returned |
| 404 | A store was not found for the provided store id |

## Queue Triggers

### SendEmail
Triggers for messages in the queue `send-email`. The messages should be in the format specified
by `WsiApi.Models.SendGrid.SendGridMessageModel`.

## Timer Triggers

### GeneratePos
Triggers at the CRONTAB expression `0 0 3 * * *`. A CSV of master PO records is pulled from the
path `dufferscorner-uri/media/WSI_PO.csv`. Another CSV of open WSI POs is pulled from
`dufferscorner-uri/media/wsi_daily_po.csv`. These two CSVs are compared and for open POs, the
CSVs are generated and queued for SFTP via `SftpBlob`.

### GenerateWsiMasterSkuList
Triggers at the CRONTAB expression `0 0 */3 * * *`. A CSV of all SKUs that should be at WSI is
pulled from `dufferscorner-uri/media/wsi_master_skus.csv`. A CSV is generated and then queued
for SFTP via `SftpBlob`.

### ProcessShippingConfirmations
Triggers at the CRONTAB expression `0 0 20 * * *`. Daily, WSI creates CSVs with tracking numbers
for orders shipped out that day. This function processes those CSVs and marks the orders as
shipped in ShipStation. Emails listed in the `recipients` property are emailed a summary
of SKUs shipped and tracking numbers for each order.