using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Pgd.Wsi.TimerTriggers
{
    public class ProcessShippingConfirmations
    {
        private readonly SftpClient wsiSftp;

        public ProcessShippingConfirmations(ConnectionInfo sftpConnectionInfo)
        {
            this.wsiSftp = new(sftpConnectionInfo);
        }

        [FunctionName("ProcessShippingConfirmations")]
        public async Task Run([TimerTrigger("0 0 20 * * *")] TimerInfo timer, ILogger log)
        {
            string csv = GetShippingConfirmations();
            csv = csv.Trim();
            string[] records = csv.Split('\n');

            foreach (string record in records)
            {
                ProcessRecord(record);
            }
        }

        private string GetShippingConfirmations()
        {
            StringBuilder csv = new();

            wsiSftp.Connect();
            List<ISftpFile> dirFiles = wsiSftp.ListDirectory("Outbound").ToList();
            wsiSftp.Disconnect();

            DateTime now = DateTime.Now;
            Regex fileMask = new($"SC_[0-9]+_[0-9]+_{now:MMddyyyy}.+csv");

            List<ISftpFile> shippingConfirmations = dirFiles.FindAll(file =>
            {
                return fileMask.IsMatch(file.Name);
            });

            foreach (SftpFile file in shippingConfirmations)
            {
                string[] lines = wsiSftp.ReadAllLines(file.FullName);
                    
                for (int i = 0; i < lines.Length; i++)
                {
                    csv.AppendLine(lines[i]);
                }
            }

            return csv.ToString();
        }

        private void ProcessRecord(string record)
        {
            string[] fields = record.Split(',');

            if (fields[0] == "CSD")
            {
                string orderNumber = fields[6];
                string sku = fields[14];
                string trackingNumber = fields[32];
            }
        }

    }
}
