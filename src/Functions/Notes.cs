using System;
using System.Threading.Tasks;
using Alejof.Notes.Functions.Impl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Alejof.Notes.Functions
{
    public static class Notes
    {
        [FunctionName("NotesGet")]
        public static async Task<IActionResult> GetNotes(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation($"C# Http trigger function executed: GET Notes");

            var notes = await BuildFunctionImpl(log)
                .GetNotes(true);

            return new OkObjectResult(notes);
        }
        
        [FunctionName("NotesGetDrafts")]
        public static async Task<IActionResult> GetDrafts(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes/drafts")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation($"C# Http trigger function executed: GET Drafts");

            var notes = await BuildFunctionImpl(log)
                .GetNotes(false);

            return new OkObjectResult(notes);
        }

        private static NotesFunction BuildFunctionImpl(ILogger log) => new NotesFunction(log, Settings.Factory.Build());
    }
}
