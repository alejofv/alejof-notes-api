using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.WindowsAzure.Storage;

namespace Alejof.Notes.Storage
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddNotesStorage(this IServiceCollection services)
        {
            // Azure Storage
            var connectionString = System.Environment.GetEnvironmentVariable($"AzureWebJobsStorage", EnvironmentVariableTarget.Process);
            var storageAccount = CloudStorageAccount.Parse(connectionString);

            services.AddSingleton(svc => storageAccount.CreateCloudTableClient());
            services.AddSingleton(svc => storageAccount.CreateCloudBlobClient());

            return services;
        }
    }
}