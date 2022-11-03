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

## HTTP Triggers

## Queue Triggers

## Timer Triggers