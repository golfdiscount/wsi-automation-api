import azure.functions as func
import azure.storage.blob as blob
import os
import pandas as pd
import requests

from io import BytesIO

def main(req: func.HttpRequest) -> func.HttpResponse:
    res: requests.Response = requests.get(os.environ['EAGLE_INV_URL'])
    res.raise_for_status()

    product_report: blob.BlobClient = blob.BlobClient.from_connection_string(os.environ['magestack_storage'], 'export', 'productReport')
    data: blob.StorageStreamDownloader = product_report.download_blob()
    magento: pd.DataFrame = pd.read_csv(BytesIO(data.readall()), header=None, index_col=0)

    eagle: pd.DataFrame = pd.read_csv(BytesIO(res.content), index_col=0)
    print(eagle.head())
    eagle.drop('Code B1', axis=1, inplace=True)

    eagle = eagle.join(magento,)
    eagle.fillna(0, inplace=True)
    print(eagle.head())
    eagle.rename(columns={
        'Item Number': 'SKU',
        'Item Description': 'Description',
        'Quantity on Hand': 'Eagle QOH',
        1: 'Magento Reserved Quantity'
    }, inplace=True)

    return func.HttpResponse(eagle.to_csv())