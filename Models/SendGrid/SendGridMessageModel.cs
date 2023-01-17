using SendGrid.Helpers.Mail;
using System.Collections.Generic;

namespace Pgd.Wsi.Models.SendGrid
{
    public class SendGridMessageModel
    {
        public List<string> To { get; set; }
        public readonly string From = "auto@golfdiscount.com";
        public string Subject { get; set; }
        public string Body { get; set; }

        // Attachments is a map of filename to a base64 encoded string of file contents
        public List<Attachment> Attachments { get; set; }
    }
}
