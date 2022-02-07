import azure.functions as func
import json
import mysql.connector as sql
import os

from azure.identity import ClientSecretCredential
from azure.keyvault.secrets import SecretClient

def main(req: func.HttpRequest) -> func.HttpResponse:
  credential = ClientSecretCredential(
      os.environ['AZURE_TENANT_ID'],
      os.environ['AZURE_CLIENT_ID'],
      os.environ['AZURE_CLIENT_SECRET']
  )
  secret_client = SecretClient(os.environ['keyvault_url'], credential)

  db: sql.MySQLConnection = sql.connect(
      user = secret_client.get_secret('db-user').value,
      password = secret_client.get_secret('db-pass').value,
      host = secret_client.get_secret('db-host').value,
      database = os.environ['db_database']
  )

  cursor = db.cursor(dictionary=True)

  qry = f"""
  SELECT store_address.`name`,
    store_address.address,
    store_address.city,
    store_address.state,
    store_address.country,
    store_address.zip
  FROM store_address
  WHERE store_address.store_num = {req.route_params.get('store_num')};
  """

  cursor.execute(qry)
  store_address = cursor.fetchone()

  if store_address is None:
    return func.HttpResponse('Could not find store', status_code=404)
  return func.HttpResponse(json.dumps(store_address), mimetype='application/json')