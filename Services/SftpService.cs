/*using Renci.SshNet;
using Renci.SshNet.Sftp;
using System;
using System.Collections.Generic;
using System.IO;

namespace Pgd.Wsi.Services
{
    public class SftpService
    {
        private readonly SftpClient sftp;
        private readonly Queue<Tuple<Stream, string>> sftpQueue;

        public SftpService(string host, string user, string pass)
        {
            sftp = new(host, user, pass);    
            sftpQueue= new Queue<Tuple<Stream, string>>();
        }

        /// <summary>
        /// Lists out the contents of the directory specified at the path
        /// </summary>
        /// <param name="path">Path to directory to list files from</param>
        /// <returns>List of SftpFile files in the directory</returns>
        public List<SftpFile> ListDirectory(string path)
        {
            Open();
            List<SftpFile> files = new(sftp.ListDirectory(path));
            sftp.Disconnect();
            return files;
        }

        /// <summary>
        /// Reads all the lines for a file at the given full path
        /// </summary>
        /// <param name="path">Full path of a file</param>
        /// <returns>Lines in a file as strings</returns>
        public string[] ReadAllLines(string path)
        {
            Open();
            string[] contents = sftp.ReadAllLines(path);
            sftp.Disconnect();
            return contents;
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
            if (sftpQueue.Count == 0)
            {
                return 0;
            }
            Open();
            int successCount = 0;

            while (sftpQueue.Count > 0)
            {
                Tuple<Stream, string> file = sftpQueue.Peek();

                // Ensure that the cursor for stream is at start to avoid empty file upload
                file.Item1.Position = 0;

                sftp.UploadFile(file.Item1, file.Item2);
                successCount++;
                sftpQueue.Dequeue();
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
*/