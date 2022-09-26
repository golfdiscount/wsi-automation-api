using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Renci.SshNet;
using System;
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
        }
    }
}
