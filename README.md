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
- SFTP_ROOT: Root directory for SFTP, should be set to "Outbound/Test" for development and staging purposes
- VAULT_URI: URI to an Azure KeyVault holding connection string, API keys, and other sensitive materials
- WSI_MASTER_SKU_URI: URI to a CSV file containing a master list of all SKUs that should be in WSI's inventory
- WSI_PO_DAILY_URI: URI to a CSV file containing current open POs from Eagle that need to be sent to WSI
- WSI_PO_MASTER_URI: URI to a CSV file containing all line items for all open POs for WSI


## KeyVault Secrets
**The following KeyVault secrets are required. Whoever is developing/debugging the application
must at a minimum have `GET` permissions for KeyVault secrets.**

- db-host: URI to a SQL Server database containing WSI information
- dufferscorner-uri: URI to root of dufferscorner website
- magento-key: API function key to Magento API
- magento-uri: URI for magento API
- sendgrid-key: SendGrid API key
- shipstation-key:
- shipstation-secret: [ShipStation API key](https://www.shipstation.com/docs/api/requirements/#authentication)
- shipstation-uri: [ShipStation API Secret](https://www.shipstation.com/docs/api/requirements/#authentication)
- wsi-pass: Password for WSI SFTP credentials
- wsi-uri: URI for WSI SFTP
- wsi-user: User for WSI SFTP credentials

# Triggers

## Blob Triggers

### SftpBlob
Triggers on the storage path `sftp/{name}` and initiates SFTP for the blob to WSI at the path
`SFTP_ROOT/{name}` where `SFTP_ROOT` is specified by an environment variable as described above.

## HTTP Triggers

### GET

#### GetOrder
Triggers on the HTTP path `orders/{orderNumber}` and returns a JSON object representing a WSI order.

##### Expected Return Codes
| Code | Description |
| ---- | ----------- |
| 200 | An order was found and returned |
| 404 | An order was not found for the provided order number |

#### GetPo
Triggers on the HTTP path `pos/{poNumber}` and returns a JSON object representing PO information.

##### Expected Return Codes
| Code | Description |
| ---- | ----------- |
| 200 | An PO was found and returned |
| 404 | An PO was not found for the provided order number |

#### GetShippingMethod
Triggers on the HTTP path `shipping/{code:alpha?}` and returns a JSON object representing
shipping method information. If `code` is not specified, returns a listing of all shipping
methods.

##### Expected Return Codes
| Code | Description |
| ---- | ----------- |
| 200 | A shipping method was found and returned |
| 404 | A shipping method was not found for the provided shipping code |

#### GetStore
Triggers on the HTTP path `stores/{id:int?}` and returns a JSON object representing
store information. if `id` is not specified, returns a listing of all stores.

##### Expected Return Codes
| Code | Description |
| ---- | ----------- |
| 200 | A store was found and returned |
| 404 | A store was not found for the provided store id |

### POST

#### PostOrder
Triggers on the path `orders` and creates a new WSI order in the database and queues it for
CSV creation at the function `OrderCsvCreation`.

| Code | Description |
| ---- | ----------- |
| 202 | The order was successfully created |
| 400 | A bad request was submitted. This could either happen because:of formatting errors, a set of required attributes was not given, or a missing Content-Type header. |

## Queue Triggers

### OrderCsvCreation
Triggers for messages in the queue `order-csv-creation`. The messages should be an order number
to generate a CSV for. Once the CSV is generated, it will be queued for SFTP via the function
`SftpBlob`.

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