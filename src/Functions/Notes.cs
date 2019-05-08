using System;
using System.Threading.Tasks;
using Alejof.Notes.Functions.Auth;
using Alejof.Notes.Functions.Impl;
using Alejof.Notes.Functions.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Alejof.Notes.Functions
{
    public static class NotesFunctionRunner
    {
        [FunctionName("NotesGet")]
        public static async Task<IActionResult> GetNotes(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation($"C# Http trigger function executed: GET Notes");

            return await FunctionRunner
                .For<Impl.NotesFunction>()
                .WithAuth(req)
                .WithLogger(log)
                .RunAsync(async f => new OkObjectResult(await f.GetNotes(true)));
        }
        
        [FunctionName("NotesGetDrafts")]
        public static async Task<IActionResult> GetDrafts(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes/drafts")] HttpRequest req,
            ILogger log)
        {                
            log.LogInformation($"C# Http trigger function executed: GET Drafts");

            return await FunctionRunner
                .For<Impl.NotesFunction>()
                .WithAuth(req)
                .WithLogger(log)
                .RunAsync(async f => new OkObjectResult(await f.GetNotes(false)));
        }
    }
}
