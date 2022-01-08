"""
Sends WSI pickticket via SFTP to WSI's filesystem
"""
import azure.functions as func
import datetime
import logging
import os
import paramiko as pm
import tempfile

from paramiko import AutoAddPolicy
from paramiko.sftp_client import SFTPClient

def main(blob: func.InputStream) -> None:
    """Processes a blob with order information and SFTPs to WSI"""
    logging.info(f'Processing {blob.name}')

    now = datetime.datetime.now()
    date_string = now.strftime(f"%m_%d_%Y_%H_%M_%S")
    client = pm.SSHClient()
    client.set_missing_host_key_policy(AutoAddPolicy())  # Automatically add host key if unknown

    logging.info('Connecting to WSI server...')
    client.connect(os.environ['WSI_HOST'], username=os.environ['WSI_USER'], password=os.environ['WSI_PASS'])
    logging.info('Connection successful')
    sftp = SFTPClient.from_transport(client.get_transport())

    logging.info(f'Uploading {blob.name} to {os.environ["target"]} directory')
    with tempfile.TemporaryFile() as order_file:
        order_file.write(blob.read())
        order_file.seek(0)
        sftp.putfo(order_file, f'/{os.environ["target"]}/PT_WSI_{date_string}.csv', confirm=False)
    logging.info('SFTP finished successfully')

    sftp.close()
    client.close()
