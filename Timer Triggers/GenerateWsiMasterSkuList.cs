using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace WsiApi.Timer_Triggers
{
    public class GenerateWsiMasterSkuList
    {
        private readonly HttpClient duffersClient;
        private readonly BlobServiceClient blobServiceClient;
        public GenerateWsiMasterSkuList(IHttpClientFactory clientFactory, BlobServiceClient blobServiceClient)
        {
            duffersClient = clientFactory.CreateClient("dufferscorner");
            this.blobServiceClient = blobServiceClient;
        }

        [FunctionName("GenerateWsiMasterSkuList")]
        public async Task Run([TimerTrigger("0 0 */3 * * *")]TimerInfo myTimer, ILogger log)
        {
            HttpResponseMessage response =  await duffersClient.GetAsync("media/wsi_master_skus.csv");
            string masterCsv = await response.Content.ReadAsStringAsync();
            string[] records = masterCsv.Trim().Replace("\"", "").Split('\n');
            records = records[1..records.Length];

            StringBuilder skuCsv = new();

            foreach (string record in records)
            {
                string[] tokens = record.Split(',');

                skuCsv.Append("SKU,I,");
                skuCsv.Append($"{tokens[0] + new string(',', 5) + tokens[1]},,");
                skuCsv.Append("HN,PGD,");
                skuCsv.Append(string.Join(',', tokens[2..5]));
                skuCsv.Append(new string(',', 6));
                skuCsv.Append("1,999,1,999,EA,PKBX,");
                skuCsv.Append(string.Join(',', tokens[5..9]));
                skuCsv.Append(new string(',', 5));
                skuCsv.Append(tokens[9]);
                skuCsv.Append(new string(',', 4));
                skuCsv.Append("N,N,N,");
                skuCsv.Append($"{tokens[10]},{tokens[11].Trim()}");
                skuCsv.Append(new string(',', 10));
                skuCsv.Append('\n');
            }

            BinaryData csvBytes = new(skuCsv.ToString());

            BlobContainerClient sftpContainer = blobServiceClient.GetBlobContainerClient("sftp");
            BlobClient skuCsvClient = sftpContainer.GetBlobClient("SKU.csv");
            await skuCsvClient.UploadAsync(csvBytes, true);
        }
    }
}
