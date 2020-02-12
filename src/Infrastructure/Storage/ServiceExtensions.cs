using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.WindowsAzure.Storage;

namespace Alejof.Notes.Storage
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddTableStorage(this IServiceCollection services)
        {
            // Azure Storage
            var connectionString = System.Environment.GetEnvironmentVariable($"AzureWebJobsStorage", EnvironmentVariableTarget.Process);

            services.AddSingleton(svc =>
            {
                var storageAccount = CloudStorageAccount.Parse(connectionString);
                return storageAccount.CreateCloudTableClient();
            });

            return services;
        }

        public static IServiceCollection AddBlobStorage(this IServiceCollection services)
        {
            // Azure Storage
            var connectionString = System.Environment.GetEnvironmentVariable($"AzureWebJobsStorage", EnvironmentVariableTarget.Process);

            services.AddSingleton(svc =>
            {
                var storageAccount = CloudStorageAccount.Parse(connectionString);
                return storageAccount.CreateCloudBlobClient();
            });

            return services;
        }
    }
}