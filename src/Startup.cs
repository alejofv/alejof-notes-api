using Alejof.Notes.Storage;
using MediatR;
using AutoMapper;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Alejof.Notes.Settings;

[assembly: FunctionsStartup(typeof(Alejof.Notes.Startup))]
namespace Alejof.Notes
{
     public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddNotesStorage();
            builder.Services.AddEnvironmentSettings();
            
            // Function dependencies
            builder.Services.AddMediatR(typeof(Startup));
            builder.Services.AddAutoMapper(typeof(Startup));
        }
    }
}
