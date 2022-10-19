using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace WsiApi.Timer_Triggers
{
    public class GeneratePos
    {
        private readonly HttpClient duffersClient;
        public GeneratePos(IHttpClientFactory clientFactory)
        {
            duffersClient = clientFactory.CreateClient("dufferscorner");
        }

        [FunctionName("GeneratePos")]
        public async Task Run([TimerTrigger("0 0 3 * * *")]TimerInfo myTimer, ILogger log)
        {
            HttpResponseMessage response = await duffersClient.GetAsync("media/WSI_PO.csv");
            string masterPos = await response.Content.ReadAsStringAsync();
            masterPos = masterPos.Trim();
            string[] masterPosRecords = masterPos.Split('\n');
            masterPosRecords = masterPosRecords[1..];

            response = await duffersClient.GetAsync("media/wsi_daily_po.csv");
            string dailyPos = await response.Content.ReadAsStringAsync();
            dailyPos = dailyPos.Trim();
            string[] dailyPosRecords = dailyPos.Split('\n');
            dailyPosRecords = dailyPosRecords[1..];

            foreach (string masterPoRecord in masterPosRecords)
            {
                string[] masterFields = masterPoRecord.Split(',');
                string masterPoNumber = masterFields[3];

                if (Array.Equals(dailyPosRecords, masterPoNumber))
                {
                    log.LogInformation(masterPoRecord);
                }
            }
        }
    }
}
