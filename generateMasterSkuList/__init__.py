import azure.functions as func
import logging
import os
import paramiko as pm
import requests
import tempfile

from azure.identity import ClientSecretCredential
from azure.keyvault.secrets import SecretClient
from paramiko import AutoAddPolicy
from paramiko.sftp_client import SFTPClient

def main(timer: func.TimerRequest) -> None:
    file: requests.Response = requests.get(os.environ['WSI_MASTER_SKU'])
    file.raise_for_status()

    file: str = bytes.decode(file.content, 'utf-8')
    file = file.split('\n')
    file = file[1:-1]  # Remove header and last row

    f = tempfile.TemporaryFile()

    for line in file:
        tokens = line.split(',')
        record = f'SKU,I,{sanitize(tokens[0])}'
        record += ',' * 5
        record += f'{sanitize(tokens[1])}'
        record += ',' * 2
        record += f'HN,PGD,{sanitize(tokens[2])},{sanitize(tokens[3])},{sanitize(tokens[4])}'
        record += ',' * 6
        record += f'1,999,1,999,EA,PKBX,{sanitize(tokens[5])},{sanitize(tokens[6])},{sanitize(tokens[7])},{sanitize(tokens[8])}'
        record += ',' * 5
        record += f'{sanitize(tokens[9])}'
        record += ',' * 4
        record += f'N,N,N,{sanitize(tokens[10])},{sanitize(tokens[11])}'
        record += ',' * 9 + '\n'

        assert(record.count(',') == 50)

        f.write(bytes(record, 'utf-8'))
    
    f.seek(0)
    upload(f)

def upload(file):
    credential = ClientSecretCredential(
        os.environ['AZURE_TENANT_ID'],
        os.environ['AZURE_CLIENT_ID'],
        os.environ['AZURE_CLIENT_SECRET']
    )
    secret_client = SecretClient(os.environ['keyvault_url'], credential)

    with pm.SSHClient() as client:
        client.set_missing_host_key_policy(AutoAddPolicy())

        logging.info('Connecting to WSI server...')
        client.connect(secret_client.get_secret('wsi-host').value,
            username=secret_client.get_secret('wsi-user').value,
            password=secret_client.get_secret('wsi-pass').value)
        logging.info('Connection successful')

        with SFTPClient.from_transport(client.get_transport()) as sftp:
            file_name = 'SKU.csv'

            logging.info(f'Uploading {file_name} to {os.environ["target"]} directory')
            remote_path = os.path.join(os.environ['target'], file_name)
            sftp.putfo(file, remote_path, confirm=True)
            logging.info('SFTP finished successfully')

def sanitize(s: str) -> str:
    """
    Sanitizes a string, removing any of the following characters:
    - ,
    """
    s = s.replace('"', '')
    s = s.replace(',', '')
    s = s.strip()
    return s 