using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System;

[assembly: FunctionsStartup(typeof(wsi_triggers.Startup))]

namespace wsi_triggers
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            DefaultAzureCredential creds = new();
            Uri keyvaultUri = new(Environment.GetEnvironmentVariable("vault-uri"));
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

            builder.Services.AddSingleton(connectionBuilder);
        }
    }
}
