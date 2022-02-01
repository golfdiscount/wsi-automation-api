"""
Sends WSI picktickets that are in the blob storage container to WSI's filesystem
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
    """Processes a blob with order information and:
    1) Inserts into WSI database
    2) SFTPs to WSI"""

    logging.info(f'Processing {blob.name}')

    with tempfile.TemporaryFile() as order:
        order.write(blob.read())
        order.seek(0)
        upload_sftp(order)


def upload_sftp(order: tempfile._TemporaryFileWrapper):
    """Uploads an order to WSI's file system in
    the directory specified by the "target"
    environment variable

    Args:
        order: A TemporaryFile containing order information in WSI's CSV format
    """
    with pm.SSHClient() as client:
        client.set_missing_host_key_policy(AutoAddPolicy())

        logging.info('Connecting to WSI server...')
        client.connect(os.environ['WSI_HOST'],
            username=os.environ['WSI_USER'],
            password=os.environ['WSI_PASS'])
        logging.info('Connection successful')

        with SFTPClient.from_transport(client.get_transport()) as sftp:
            now = datetime.datetime.now()
            date_string = now.strftime(f"%m_%d_%Y_%H_%M_%S")
            file_name = f'PT_WSI_{date_string}'

            logging.info(f'Uploading {file_name} to {os.environ["target"]} directory')
            remote_path = os.path.join(os.environ["target"], file_name)
            sftp.putfo(order, remote_path, confirm=False)
            logging.info('SFTP finished successfully')
