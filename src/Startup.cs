using Alejof.Notes.Storage;
using MediatR;
using AutoMapper;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(Alejof.Notes.Startup))]
namespace Alejof.Notes
{
     public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddTableStorage();
            
            // Function dependencies
            builder.Services.AddMediatR(typeof(Startup));
            builder.Services.AddAutoMapper(typeof(Startup));
        }
    }
}