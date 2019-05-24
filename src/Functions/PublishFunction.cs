using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alejof.Notes.Extensions;
using Alejof.Notes.Functions.Auth;
using Alejof.Notes.Functions.Infrastructure;
using Alejof.Notes.Functions.Mapping;
using Alejof.Notes.Functions.TableStorage;
using Alejof.Notes.Models;
using Alejof.Notes.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Functions
{
    public class PublishFunction : IFunction
    {
        public ILogger Log { get; set; }
        public FunctionSettings Settings { get; set; }

        public const string RedeployQueueName = "netlify-deploy-signal";

        public async Task<Result> Publish(string id, bool publish)
        {
            var table = GetTable(NoteEntity.TableName);

            var existingKey = NoteEntity.GetDefaultKey(!publish);
            var existingEntity = await table.RetrieveAsync<NoteEntity>(existingKey, id);

            if (existingEntity == null)
                return id.AsFailedResult("NotFound");

            var newEntity = NoteEntity
                .New(publish, DateTime.UtcNow)
                .CopyModel(existingEntity.ToModel());
            newEntity.BlobUri = existingEntity.BlobUri;

            var result = await table.InsertAsync(newEntity);
            if (!result)
                return id.AsFailedResult("InsertAsync failed");

            await table.DeleteAsync(existingEntity);
            return Result.Ok;
        }
        
        private CloudTable GetTable(string tableName)
        {
            var storageAccount = CloudStorageAccount.Parse(Settings.StorageConnectionString);
            var tableClient = storageAccount.CreateCloudTableClient();
            
            return tableClient.GetTableReference(tableName);
        }
        
        // Azure Functions

        [FunctionName("Publish")]
        public static async Task<IActionResult> PublishNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "delete", Route = "publish/{id}")] HttpRequest req, ILogger log, string id,
            [Queue(RedeployQueueName)]IAsyncCollector<string> redeploySignalCollector)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(PublishFunction)}, method: {req.Method}");

            var publish = !string.Equals(req.Method, "delete", StringComparison.OrdinalIgnoreCase);

            return await HttpRunner.For<PublishFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(
                    async function =>
                    {
                        var publishResult = await function.Publish(id, publish);

                        // TODO: MULTI-TENANT ARCHITECTURE:

                        // Get tenant name from function's AuthContext instead of settings
                        // Find mapped site name from tableStorage (PK:deploy-hook, RK:tenant)

                        if (publishResult.Success && !string.IsNullOrEmpty(function.Settings.ContentSiteName))
                            await redeploySignalCollector.AddAsync(function.Settings.ContentSiteName);

                        return publishResult;
                    })
                .AsIActionResult(x => new OkResult());
        }
    }
}
