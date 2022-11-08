using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using System;
using System.IO;

namespace WsiApi.Blob_Triggers
{
    public class SftpBlob
    {
        private readonly SftpClient sftp;
        public SftpBlob(SftpClient sftp)
        {
            this.sftp = sftp;
        }

        [FunctionName("SftpBlob")]
        public void Run([BlobTrigger("sftp/{name}", Connection = "AzureWebJobsStorage")]Stream blob, string name, ILogger log)
        {
            log.LogInformation($"Initializing SFTP for {name}");
            sftp.Connect();

            try
            {
                sftp.UploadFile(blob, $"Inbound/{name}");
            } catch(Exception e)
            {
                log.LogInformation(e.Message);
            } finally
            {
                sftp.Disconnect();
            }
            
        }
    }
}
