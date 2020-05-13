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
    public class ApiFunctions
    {
        private readonly IMediator _mediator;

        public ApiFunctions(
            IMediator mediator)
        {
            this._mediator = mediator;
        }

        private async Task<Handlers.Auth.Identity?> Authenticate(HttpRequest req, ILogger log, [CallerMemberName]string memberName = "")
        {
            // Authenticate
            log.LogInformation($" => {memberName} authenticating.");

            var (identity, msg) = await _mediator.Send(new Handlers.Auth.Request(req));
            if (identity == null)
                log.LogInformation(msg);

            return identity;
        }

        private async Task<TResponse> ProcessActionRequest<TRequest, TResponse>(Handlers.Auth.Identity identity, TRequest request)
            where TRequest : IRequest<TResponse>, Handlers.IAuditableRequest
            where TResponse : Handlers.ActionResponse
        {
            var result = await _mediator.Send<TResponse>(request);

            await _mediator.Publish(
                new Handlers.Audit.Notification(identity, request, result as Handlers.ActionResponse));

            return result;
        }

        private Task<Handlers.ActionResponse> ProcessActionRequest<TRequest>(Handlers.Auth.Identity identity, TRequest request)
            where TRequest : IRequest<Handlers.ActionResponse>, Handlers.IAuditableRequest
        {
            return this.ProcessActionRequest<TRequest, Handlers.ActionResponse>(identity, request);
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
            request.Format = req.GetFormatQueryParam();
            
            var result = await this.ProcessActionRequest<Handlers.CreateNote.Request, Handlers.CreateNote.Response>(identity, request);
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

            request.TenantId = identity.TenantId;
            request.NoteId = id;
            request.Format = req.GetFormatQueryParam();
            
            var result = await this.ProcessActionRequest(identity, request);

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

            var result = await this.ProcessActionRequest(identity,
                new Handlers.DeleteNote.Request
                {
                    TenantId = identity.TenantId,
                    NoteId = id
                });

            if (!result.Success)
                return new ConflictObjectResult(result);

            return new OkObjectResult(result);
        }

        [FunctionName("MediaGetAll")]
        public async Task<IActionResult> GetMediaFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "media")] HttpRequest req, ILogger log)
        {
            var identity = await this.Authenticate(req, log);
            if (identity == null)
                return new UnauthorizedResult();

            var result = await _mediator.Send(
                new Handlers.GetMedia.Request
                {
                    TenantId = identity.TenantId,
                });
            
            return new OkObjectResult(result.Data);
        }

        [FunctionName("MediaUpload")]
        public async Task<IActionResult> UploadMediaFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "media")] HttpRequest req, ILogger log, IBinder binder,
            [Queue("media-thumbnail-signal", Connection = "StorageConnectionString")]IAsyncCollector<string> thumbnailSignalCollector)
        {
            var identity = await this.Authenticate(req, log);
            if (identity == null)
                return new UnauthorizedResult();

            var header = (string)req.Headers["Notes-Media-Name"];
            if (string.IsNullOrEmpty(header))
                return new BadRequestResult();

            var result = await this.ProcessActionRequest<Handlers.CreateMedia.Request, Handlers.CreateMedia.Response>(identity,
                new Handlers.CreateMedia.Request
                {
                    TenantId = identity.TenantId,
                    Name = $"{System.IO.Path.GetFileNameWithoutExtension(header)}-{DateTime.UtcNow.ToString("yyMMddhhmmss")}{System.IO.Path.GetExtension(header)}",
                    Content = req.Body,
                });

            if (!result.Success)
                return new ConflictObjectResult(result);

            await thumbnailSignalCollector.AddAsync(result.Path);
            return new OkObjectResult(result);
        }

        [FunctionName("MediaDelete")]
        public async Task<IActionResult> DeleteMediaFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "media/{id}")] HttpRequest req, ILogger log, string id)
        {
            var identity = await this.Authenticate(req, log);
            if (identity == null)
                return new UnauthorizedResult();

            var result = await this.ProcessActionRequest(identity,
                new Handlers.DeleteMedia.Request
                {
                    TenantId = identity.TenantId,
                    MediaId = id,
                });

            if (!result.Success)
                return new ConflictObjectResult(result);

            return new OkObjectResult(result);
        }

        [FunctionName("Publish")]
        public async Task<IActionResult> PublishNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "delete", Route = "publish/{id}")] HttpRequest req, ILogger log, string id,
            [Queue("netlify-deploy-signal", Connection = "StorageConnectionString")]IAsyncCollector<string> deploySignalCollector)
        {
            var identity = await this.Authenticate(req, log);
            if (identity == null)
                return new UnauthorizedResult();

            var result = await this.ProcessActionRequest(identity,
                new Handlers.PublishNote.Request
                {
                    TenantId = identity.TenantId,
                    NoteId = id,
                    Publish = string.Equals(req.Method, "post", StringComparison.OrdinalIgnoreCase),
                });

            if (!result.Success)
                return new ConflictObjectResult(result);

            await deploySignalCollector.AddAsync(identity.TenantId);
            return new OkObjectResult(result);
        }
    }
}
