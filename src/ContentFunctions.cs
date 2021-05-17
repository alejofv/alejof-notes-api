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
    }
}
