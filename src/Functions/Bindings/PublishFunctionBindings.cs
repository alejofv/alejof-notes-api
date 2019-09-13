using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alejof.Notes.Extensions;
using Alejof.Notes.Functions.Infrastructure;
using Alejof.Notes.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Alejof.Notes.Functions.Bindings
{
    public class PublishFunctionBindings
    {
        public const string RedeployQueueName = "netlify-deploy-signal";

        [FunctionName("Publish")]
        public static async Task<IActionResult> PublishNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "delete", Route = "publish/{id}")] HttpRequest req, ILogger log, string id,
            [Queue(RedeployQueueName)]IAsyncCollector<string> redeploySignalCollector)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(PublishFunction)}, method: {req.Method}");

            var publish = !string.Equals(req.Method, "delete", StringComparison.OrdinalIgnoreCase);

            return await HttpRunner.For<PublishFunction>(log)
                .WithAuthentication(req)
                .ExecuteAsync(
                    async function =>
                    {
                        var publishResult = await function.Publish(id, publish);

                        if (publishResult.Success)
                            await redeploySignalCollector.AddAsync(function.AuthContext.TenantId);

                        return publishResult;
                    })
                .AsIActionResult<Result>(x => new OkResult());
        }        
    }
}
