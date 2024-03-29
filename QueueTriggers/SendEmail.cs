using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using SendGrid.Helpers.Mail;
using System.Text.Json;
using System.Threading.Tasks;
using Pgd.Wsi.Models.SendGrid;
using SendGrid;

namespace Pgd.Wsi.QueueTriggers
{
    public class SendEmail
    {
        private readonly JsonSerializerOptions jsonOptions;
        private readonly ISendGridClient emailClient;
        public SendEmail(JsonSerializerOptions jsonSerializerOptions, ISendGridClient sendGridClient)
        {
            jsonOptions = jsonSerializerOptions;
            emailClient = sendGridClient;
        }

        [FunctionName("SendEmail")]
        public async Task Run(
            [QueueTrigger("send-email", Connection = "AzureWebJobsStorage")]string myQueueItem,
            ILogger log)
        {
            log.LogInformation($"C# Queue trigger function processed: {myQueueItem}");

            SendGridMessageModel incomingMessage = JsonSerializer.Deserialize<SendGridMessageModel>(myQueueItem, jsonOptions);

            SendGridMessage message = new();

            foreach (string recipient in incomingMessage.To)
            {
                EmailAddress recipientAddress = new(recipient);
                message.AddTo(recipientAddress);
            }

            message.AddContent("text/html", incomingMessage.Body);
            message.SetFrom(new EmailAddress(incomingMessage.From));
            message.SetSubject(incomingMessage.Subject);

            if (incomingMessage.Attachments != null)
            {
                foreach (Attachment attachment in incomingMessage.Attachments)
                {
                    message.AddAttachment(attachment);
                }
            }

            Response sendResponse = await emailClient.SendEmailAsync(message);

            if(!sendResponse.IsSuccessStatusCode)
            {
                log.LogError("Unable to send email");
                log.LogError(await sendResponse.Body.ReadAsStringAsync());
            }
        }
    }
}
