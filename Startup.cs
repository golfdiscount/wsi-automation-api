using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Renci.SshNet;
using System;
using System.Text;
using System.Text.Json;

[assembly: FunctionsStartup(typeof(wsi_triggers.Startup))]

namespace wsi_triggers
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            DefaultAzureCredential creds = new();
            Uri keyvaultUri = new(Environment.GetEnvironmentVariable("VAULT_URI"));
            SecretClient secretClient = new(keyvaultUri, creds);

            KeyVaultSecret dbHost = secretClient.GetSecret("db-host");
            string host = dbHost.Value;

            SqlConnectionStringBuilder connectionBuilder = new()
            {
                DataSource = host,
                Pooling = true,
                MinPoolSize = 3,
                InitialCatalog = "wsi",
                Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault
            };

            JsonSerializerOptions jsonOptions = new()
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            KeyVaultSecret wsiHost = secretClient.GetSecret("wsi-uri");
            KeyVaultSecret wsiUser = secretClient.GetSecret("wsi-user");
            KeyVaultSecret wsiPass = secretClient.GetSecret("wsi-pass");
            SftpClient sftp = new(wsiHost.Value, wsiUser.Value, wsiPass.Value);

            builder.Services.AddSingleton(jsonOptions);
            builder.Services.AddSingleton(connectionBuilder);
            builder.Services.AddSingleton(sftp);

            builder.Services.AddHttpClient("magento", config =>
            {
                KeyVaultSecret magentoUri = secretClient.GetSecret("magento-uri");
                KeyVaultSecret magentoKey = secretClient.GetSecret("magento-key");

                config.BaseAddress = new(magentoUri.Value);
                config.DefaultRequestHeaders.Add("x-functions-key", magentoKey.Value);
            });
            builder.Services.AddHttpClient("dufferscorner", config =>
            {
                KeyVaultSecret duffersUri = secretClient.GetSecret("dufferscorner-uri");

                config.BaseAddress = new(duffersUri.Value);
            });
            builder.Services.AddHttpClient("shipstation", config =>
            {
                KeyVaultSecret shipstationUri = secretClient.GetSecret("shipstation-uri");
                KeyVaultSecret shipstationKey = secretClient.GetSecret("shipstation-key");
                KeyVaultSecret shipstationSecret = secretClient.GetSecret("shipstation-secret");

                config.BaseAddress = new(shipstationUri.Value);

                string shipstationCreds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{shipstationKey.Value}:{shipstationSecret.Value}"));
                config.DefaultRequestHeaders.Authorization = new("Basic", shipstationCreds);
            });

            builder.Services.AddAzureClients(clientBuilder =>
            {
                clientBuilder.AddBlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
                clientBuilder.AddQueueServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage")).ConfigureOptions(config =>
                {
                    config.MessageEncoding = Azure.Storage.Queues.QueueMessageEncoding.Base64;
                });
            });
        }
    }
}
