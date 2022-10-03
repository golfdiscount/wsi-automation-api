using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using SendGrid.Helpers.Mail;
using SendGrid.Extensions.DependencyInjection;
using System.Text.Json;
using System.Threading.Tasks;
using wsi_triggers.Models.SendGrid;
using SendGrid;

namespace wsi_triggers.Queue_Triggers
{
    public class SendEmail
    {
        private readonly JsonSerializerOptions jsonOptions;
        private readonly SendGridClient emailClient;
        public SendEmail(JsonSerializerOptions jsonSerializerOptions, SendGridClient sendGridClient)
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
            message.AddTo(incomingMessage.To);
            message.AddContent("text/html", incomingMessage.Body);
            message.SetFrom(new EmailAddress(incomingMessage.From));
            message.SetSubject(incomingMessage.Subject);

            await emailClient.SendEmailAsync(message);
        }
    }
}
