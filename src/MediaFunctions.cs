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
    public class MediaFunctions
    {
        private readonly Auth.Authenticator _authenticator;
        private readonly IMediator _mediator;

        public MediaFunctions(
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
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "media")] HttpRequest req, ILogger log,
            [Queue("media-thumbnail-signal", Connection = "StorageConnectionString")]IAsyncCollector<string> thumbnailSignalCollector)
        {
            var identity = await this.Authenticate(req, log);
            if (identity == null)
                return new UnauthorizedResult();

            var header = (string)req.Headers["Notes-Media-Name"];
            if (string.IsNullOrEmpty(header))
                return new BadRequestResult();

            var result = await _mediator.Send(
                new Handlers.CreateMedia.Request
                {
                    TenantId = identity.TenantId,
                    Name = $"{System.IO.Path.GetFileNameWithoutExtension(header)}-{DateTime.UtcNow.ToString("yyMMddhhmmss")}{System.IO.Path.GetExtension(header)}",
                    Content = req.Body,
                },
                identity);

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

            var result = await _mediator.Send(
                new Handlers.DeleteMedia.Request
                {
                    TenantId = identity.TenantId,
                    MediaId = id,
                },
                identity);

            if (!result.Success)
                return new ConflictObjectResult(result);

            return new OkObjectResult(result);
        }
    }
}
