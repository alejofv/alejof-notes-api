#nullable enable

using System;
using Microsoft.Extensions.DependencyInjection;

namespace Alejof.Notes.Settings
{
    public class EnvironmentSettings
    {
        public const string Development = "Development";

        public string EnvironmentName { get; set; } = "";
        public bool IsDevelopment => EnvironmentName == Development;

    }

    public static class ServiceExtensions
    {
        public static IServiceCollection AddEnvironmentSettings(this IServiceCollection services)
        {
            var environmentName = System.Environment.GetEnvironmentVariable($"FUNCTIONS_ENVIRONMENT", EnvironmentVariableTarget.Process);
            services.AddSingleton(svc => new EnvironmentSettings { EnvironmentName = environmentName ?? EnvironmentSettings.Development } );

            return services;
        }
    }
}
