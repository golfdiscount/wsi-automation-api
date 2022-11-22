using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using System;
using System.IO;
using System.Threading.Tasks;

namespace WsiApi.Blob_Triggers
{
    public class SftpBlob
    {
        private readonly BlobServiceClient blobServiceClient;
        private readonly SftpClient sftp;

        public SftpBlob(SftpClient sftp, BlobServiceClient blobServiceClient)
        {
            this.blobServiceClient = blobServiceClient;
            this.sftp = sftp;
        }

        [FunctionName("SftpBlob")]
        public async Task Run([BlobTrigger("sftp/{name}", Connection = "AzureWebJobsStorage")]Stream blob, string name, ILogger log)
        {
            log.LogInformation($"Initializing SFTP for {name}");

            if (!sftp.IsConnected) {
                sftp.Connect();
            }

            try
            {
                sftp.UploadFile(blob, $"Inbound/{name}");
                BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient("sftp");
                log.LogInformation($"Deleting {name} from sftp container");
                await blobContainerClient.DeleteBlobAsync(name);

            } catch(Exception e)
            {
                log.LogError(e.Message);
            } finally
            {
                sftp.Disconnect();
            }
            
        }
    }
}
