import azure.functions as func
import logging
import os
import paramiko as pm
import re
import tempfile

from azure.identity import ClientSecretCredential
from azure.keyvault.secrets import SecretClient
from io import StringIO
from paramiko import AutoAddPolicy
from paramiko.sftp_client import SFTPClient

def main(blob: func.InputStream) -> None:
    credential = ClientSecretCredential(
        os.environ['AZURE_TENANT_ID'],
        os.environ['AZURE_CLIENT_ID'],
        os.environ['AZURE_CLIENT_SECRET']
    )
    secret_client = SecretClient(os.environ['keyvault_url'], credential)

    logging.info(f'Processing {blob.name}')

    with pm.SSHClient() as client:
        client.set_missing_host_key_policy(AutoAddPolicy())

        logging.info('Connecting to WSI server...')
        client.connect(secret_client.get_secret('wsi-host').value,
            username=secret_client.get_secret('wsi-user').value,
            password=secret_client.get_secret('wsi-pass').value)
        logging.info('Connection successful')

        with SFTPClient.from_transport(client.get_transport()) as sftp:
            file_name = re.search('RO.+', blob.name).group(0)

            logging.info(f'Uploading {file_name} to {os.environ["target"]} directory')
            remote_path = os.path.join(os.environ['target'], file_name)
            sftp.putfo(blob, remote_path, confirm=True)
            logging.info('SFTP finished successfully')