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
    public class PublishFunction : IAuthorizedFunction
    {
        public AuthContext AuthContext { get; set; }
        public ILogger Log { get; set; }
        public FunctionSettings Settings { get; set; }
        
        public const string RedeployQueueName = "netlify-deploy-signal";

        public async Task<Result> Publish(string id, bool publish)
        {
            var table = GetTable(NoteEntity.TableName);

            var existingKey = NoteEntity.GetKey(this.AuthContext.TenantId, !publish);
            var existingEntity = await table.RetrieveAsync<NoteEntity>(existingKey, id);

            if (existingEntity == null)
                return id.AsFailedResult("NotFound");

            var newEntity = NoteEntity
                .New(this.AuthContext.TenantId, publish, DateTime.UtcNow)
                .CopyModel(existingEntity.ToModel());
            newEntity.BlobUri = existingEntity.BlobUri;

            var result = await table.InsertAsync(newEntity);
            if (!result)
                return id.AsFailedResult("InsertAsync failed");

            await table.DeleteAsync(existingEntity);
            return Result.Ok;
        }

        public async Task<string> GetDeploySiteName()
        {
            var table = GetTable(ConfigEntity.TableName);
            var mapping = await table.RetrieveAsync<ConfigEntity>(this.AuthContext.TenantId, ConfigEntity.DeploySignalKey);

            return mapping?.Value;
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

            return await HttpRunner.For<PublishFunction>(log)
                .WithAuthentication(req)
                .ExecuteAsync(
                    async function =>
                    {
                        var publishResult = await function.Publish(id, publish);

                        // MULTI-TENANT ARCHITECTURE:

                        if (publishResult.Success)
                        {
                            // Get tenant name from function's AuthContext and 
                            // Find mapped site name from tableStorage (PK:deploy, RK:tenant)   
                            var deploySiteName = await function.GetDeploySiteName();
                            if (!string.IsNullOrEmpty(deploySiteName))
                                await redeploySignalCollector.AddAsync(deploySiteName);
                        }

                        return publishResult;
                    })
                .AsIActionResult<Result>(x => new OkResult());
        }        
    }
}
