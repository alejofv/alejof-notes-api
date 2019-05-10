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
    public class ContentFunction : IFunction
    {
        public ILogger Log { get; set; }
        public FunctionSettings Settings { get; set; }

        public async Task<IReadOnlyCollection<Content>> GetContent()
        {
            var table = GetTable(NoteEntity.TableName);

            var publishedKey = NoteEntity.GetDefaultKey(true);
            Log.LogInformation($"Getting notes from storage. TableName: {NoteEntity.TableName}, Key: {publishedKey}");

            var notes = await table.ScanAsync<NoteEntity>(publishedKey);
            return notes
                .Select(n => n.ToContentModel())
                .ToList()
                .AsReadOnly();
        }
        
        private CloudTable GetTable(string tableName)
        {
            var storageAccount = CloudStorageAccount.Parse(Settings.StorageConnectionString);
            var tableClient = storageAccount.CreateCloudTableClient();
            
            return tableClient.GetTableReference(tableName);
        }
        
        // Azure Functions

        [FunctionName("NotesGetContent")]
        public static async Task<IActionResult> GetContentFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "content")] HttpRequest req, ILogger log)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(GetContentFunction)}");

            return await HttpRunner.For<ContentFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.GetContent())
                .AsIActionResult();
        }
    }
}
