using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WsiApi.Services
{
    public class SftpService
    {
        private readonly SftpClient sftp;
        public SftpService(string host, string user, string pass)
        {
            sftp = new(host, user, pass);    
        }
    }
}
