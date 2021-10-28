"""
Uploads WSI orders to a MySQL database on Azure
"""
import azure.functions as func
from .OrderImporter import OrderImporter

def main(req: func.HttpRequest) -> func.HttpResponse:
    """Process files, insert them into the DB, and SFTP to WSI"""
    importer = OrderImporter()

    file = req.fiels.get("file")

    if file is not None:
        importer.process_fo(file)
    else:
        body = req.get_body()
        body = bytes.decode(body)
        importer.process_bytes(body)

    importer.trigger_upload_flow()

    return func.HttpResponse("Finished trigger run", status_code=200)
