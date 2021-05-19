#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Alejof.Notes.Extensions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Alejof.Notes
{
    public class ContentFunctions
    {
        private readonly IMediator _mediator;

        public ContentFunctions(
            IMediator mediator)
        {
            this._mediator = mediator;
        }

        [FunctionName("ContentGet")]
        public async Task<IActionResult> GetContent(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "content/{tenantId}")] HttpRequest req, ILogger log, string tenantId)
        {
            log.LogInformation($"Fetching content for {tenantId}.");

            Enum.TryParse<Handlers.ContentFormat>(req.GetQueryParam("format"), ignoreCase: true, out var format);

            var result = await _mediator.Send(new Handlers.Content.Request { TenantId = tenantId, Format = format });
            return new OkObjectResult(result.ContentList);
        }

        [FunctionName("ContentPublishRequest")]
        public async Task<IActionResult> RequestPublishContent(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "content/{tenantId}/republish")] HttpRequest req, ILogger log, string tenantId,
            [Queue("notes-republish-signal", Connection = "StorageConnectionString")]IAsyncCollector<Handlers.PublishNote.Request> publishSignalCollector)
        {
            log.LogInformation($"Re-publishing notes for {tenantId}.");

            var notesRequest = await _mediator.Send(
                new Handlers.GetNote.AllNotesRequest
                {
                    TenantId = tenantId,
                    Published = true,
                });

            Enum.TryParse<Handlers.PublishFormat>(req.GetQueryParam("format"), ignoreCase: true, out var format);
            foreach (var note in notesRequest.Data)
            {
                await publishSignalCollector.AddAsync(
                    new Handlers.PublishNote.Request{
                        TenantId = tenantId,
                        NoteId = note.Id,
                        Format = format,
                        Publish = true,
                    });
            }

            return new OkResult();
        }

        [FunctionName("ContentPublish")]
        public async Task PublishContent(
            [QueueTrigger("notes-republish-signal", Connection = "StorageConnectionString")] Handlers.PublishNote.Request request, ILogger log)
        {
            log.LogInformation($"Re-publishing note {request.TenantId}/{request.NoteId}");

            var result = await _mediator.Send(request);

            log.LogInformation($"Re-publishing note {request.TenantId}/{request.NoteId}: {(result.Success ? "OK" : "Error")} {result.Message}");
        }
    }
}
