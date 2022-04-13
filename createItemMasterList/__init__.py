from dataclasses import dataclass
import azure.functions as func
import mysql.connector as sql
import os

def main(req: func.HttpRequest) -> func.HttpResponse:
    
    db: sql.MySQLConnection = sql.connect(
        user = 'dbread',
        password = os.environ['EAGLE_PASS'],
        host = os.environ['EAGLE_HOST'],
        database = os.environ['EAGLE_DB']
    )

    cursor = db.cursor()

    qry = 'SELECT * FROM dw_store;'

    cursor.execute(qry)

    for row in cursor:
        for column in row:
            print(column)
        print('-' * 50)

    return func.HttpResponse('hello')