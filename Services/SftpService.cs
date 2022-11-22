using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;

namespace WsiApi.Services
{
    public class SftpService
    {
        private readonly SftpClient sftp;
        private readonly Queue<Tuple<Stream, string>> sftpQueue;

        public SftpService(string host, string user, string pass)
        {
            sftp = new(host, user, pass);    
        }

        /// <summary>
        /// Queues a file to be uploaded
        /// </summary>
        /// <param name="path">Path to the file on the remote system</param>
        /// <param name="content">File contents</param>
        /// <exception cref="ArgumentException">Path or content are either null or have length of 0</exception>
        public void Queue(string path, Stream content)
        {
            if (path == null || path.Length== 0)
            {
                throw new ArgumentException("The file path must be longer than 0 characters", nameof(path));
            }

            if (content == null || content.Length == 0)
            {
                throw new ArgumentException("Content cannot be null or have a length of 0", nameof(content));
            }

            sftpQueue.Enqueue(Tuple.Create(content, path));
        }


        /// <summary>
        /// Uploads all files in queue
        /// </summary>
        /// <returns>Count of files that were successfully uploaded</returns>
        public int UploadQueue()
        {
            Open();
            int successCount = 0;

            while (sftpQueue.Count > 0)
            {
                Tuple<Stream, string> file = sftpQueue.Peek();

                sftp.UploadFile(file.Item1, file.Item2);
                successCount++;
            }

            sftp.Disconnect();
            return successCount;
        }

        /// <summary>
        /// Opens an SSH tunnel to the server if it is not already open
        /// </summary>
        private void Open()
        {
            if (!sftp.IsConnected)
            {
                sftp.Connect();
            }
        }
    }
}
