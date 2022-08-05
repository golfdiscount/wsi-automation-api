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

            string user;

            try
            {
                KeyVaultSecret dbUser = secretClient.GetSecret("db-user");

                user = dbUser.Value;
            } 
            catch (Azure.RequestFailedException)
            {
                user = Environment.GetEnvironmentVariable("db-user");
            }

            SqlConnectionStringBuilder connectionBuilder = new()
            {
                DataSource = host,                
                UserID = user,
                Pooling = true,
                MinPoolSize = 3,
                InitialCatalog = "wsi",
                Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault
            };

            builder.Services.AddSingleton(connectionBuilder);
        }
    }
}
