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
    public class NoteFunctions
    {
        private readonly Auth.Authenticator _authenticator;
        private readonly IMediator _mediator;

        public NoteFunctions(
            Auth.Authenticator authenticator,
            IMediator mediator)
        {
            _authenticator = authenticator;
            _mediator = mediator;
        }

        private async Task<Auth.Identity?> Authenticate(HttpRequest req, ILogger log, [CallerMemberName]string memberName = "")
        {
            // Authenticate
            log.LogInformation($" => {memberName} authenticating.");

            var (identity, msg) = await _authenticator.Authenticate(req);
            if (identity == null)
                log.LogInformation(msg);

            return identity;
        }

        [FunctionName("NotesGetAll")]
        public async Task<IActionResult> GetNotes(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes")] HttpRequest req, ILogger log)
        {
            var identity = await this.Authenticate(req, log);
            if (identity == null)
                return new UnauthorizedResult();

            var result = await _mediator.Send(
                new Handlers.GetNote.AllNotesRequest
                {
                    TenantId = identity.TenantId,
                    Published = req.GetPublishedQueryParam()
                });
            
            return new OkObjectResult(result.Data);
        }

        [FunctionName("NotesGet")]
        public async Task<IActionResult> GetNote(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes/{id}")] HttpRequest req, ILogger log, string id)
        {
            var identity = await this.Authenticate(req, log);
            if (identity == null)
                return new UnauthorizedResult();

            var result = await _mediator.Send(
                new Handlers.GetNote.SingleNoteRequest
                {
                    TenantId = identity.TenantId,
                    NoteId = id,
                });

            if (!result.Data.Any())
                return new NotFoundResult();

            return new OkObjectResult(result.Data.First());
        }

        [FunctionName("NotesCreate")]
        public async Task<IActionResult> CreateNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "notes")] HttpRequest req, ILogger log)
        {
            var identity = await this.Authenticate(req, log);
            if (identity == null)
                return new UnauthorizedResult();

            var request = await req.GetJsonBodyAs<Handlers.CreateNote.Request>();
            if (request == null)
                return new BadRequestResult();

            request.TenantId = identity.TenantId;
            request.Format = req.GetQueryParam("format") ?? "md";
            
            var result = await _mediator.Send(request, identity);
            if (!result.Success)
                return new ConflictObjectResult(result);

            return new OkObjectResult(result);
        }

        [FunctionName("NotesEdit")]
        public async Task<IActionResult> EditNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "notes/{id}")] HttpRequest req, ILogger log, string id)
        {
            var identity = await this.Authenticate(req, log);
            if (identity == null)
                return new UnauthorizedResult();

            var request = await req.GetJsonBodyAs<Handlers.EditNote.Request>();
            if (request == null)
                return new BadRequestResult();

            request.NoteId = id;
            request.TenantId = identity.TenantId;
            request.Format = req.GetQueryParam("format") ?? "md";
            
            var result = await _mediator.Send(request, identity);

            if (!result.Success)
                return new ConflictObjectResult(result);
                
            return new OkObjectResult(result);
        }

        [FunctionName("NotesDelete")]
        public async Task<IActionResult> DeleteNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "notes/{id}")] HttpRequest req, ILogger log, string id)
        {
            var identity = await this.Authenticate(req, log);
            if (identity == null)
                return new UnauthorizedResult();

            var result = await _mediator.Send(
                new Handlers.DeleteNote.Request
                {
                    TenantId = identity.TenantId,
                    NoteId = id
                },
                identity);

            if (!result.Success)
                return new ConflictObjectResult(result);

            return new OkObjectResult(result);
        }

        [FunctionName("NotesPublish")]
        public async Task<IActionResult> PublishNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "delete", Route = "publish/{id}")] HttpRequest req, ILogger log, string id,
            [Queue("netlify-deploy-signal", Connection = "StorageConnectionString")]IAsyncCollector<string> deploySignalCollector)
        {
            var identity = await this.Authenticate(req, log);
            if (identity == null)
                return new UnauthorizedResult();

            Enum.TryParse<Handlers.PublishFormat>(req.GetQueryParam("format"), ignoreCase: true, out var format);

            var result = await _mediator.Send(
                new Handlers.PublishNote.Request
                {
                    TenantId = identity.TenantId,
                    NoteId = id,
                    Format = format, // TODO: Make this tenant-dependent
                    Publish = string.Equals(req.Method, "post", StringComparison.OrdinalIgnoreCase),
                },
                identity);

            if (!result.Success)
                return new ConflictObjectResult(result);

            await deploySignalCollector.AddAsync(identity.TenantId);
            return new OkObjectResult(result);
        }
    }
}
