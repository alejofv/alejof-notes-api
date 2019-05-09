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
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "publish/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(PublishFunction)}");

            return await HttpRunner.For<PublishFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.Publish(id, true));
        }

        [FunctionName("Unpublish")]
        public static async Task<IActionResult> UnpublishFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "unpublish/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(UnpublishFunction)}");

            return await HttpRunner.For<PublishFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.Publish(id, false));
        }
    }
}
