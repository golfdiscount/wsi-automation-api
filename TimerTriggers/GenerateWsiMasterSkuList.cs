using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Pgd.Wsi.Services;

namespace Pgd.Wsi.TimerTriggers
{
    public class GenerateWsiMasterSkuList
    {
        private readonly HttpClient duffersClient;
        private readonly SftpService _wsiSftp;
        public GenerateWsiMasterSkuList(IHttpClientFactory clientFactory, SftpService wsiSftp)
        {
            duffersClient = clientFactory.CreateClient("dufferscorner");
            _wsiSftp= wsiSftp;
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
                skuCsv.AppendLine();
            }
            Stream fileContents = new MemoryStream();
            StreamWriter writer = new(fileContents);
            writer.Write(skuCsv);
            writer.Flush();

            _wsiSftp.Queue("Inbound/SKU.csv", fileContents);
            int uploadCount = _wsiSftp.UploadQueue();

            log.LogInformation($"Uploaded {uploadCount} file(s) to WSI");
        }
    }
}
