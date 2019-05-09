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
    public class DraftsFunction : IFunction
    {
        public ILogger Log { get; set; }
        public FunctionSettings Settings { get; set; }

        public async Task<IReadOnlyCollection<Note>> GetDrafts()
        {
            var table = GetTable(NoteEntity.TableName);

            var draftKey = NoteEntity.GetDefaultKey(false);
            Log.LogInformation($"Getting notes from storage. TableName: {NoteEntity.TableName}, Key: {draftKey}");

            var notes = await table.ScanAsync<NoteEntity>(draftKey);
            return notes
                .Select(n => n.ToListModel())
                .ToList()
                .AsReadOnly();
        }

        public async Task<Note> GetDraft(string id)
        {
            var table = GetTable(NoteEntity.TableName);

            var draftKey = NoteEntity.GetDefaultKey(false);
            var draft = await table.RetrieveAsync<NoteEntity>(draftKey, id);

            return draft?.ToModel();
        }

        public async Task<Result> CreateDraft(Note draft)
        {
            var table = GetTable(NoteEntity.TableName);

            var entity = NoteEntity
                .New(false, DateTime.UtcNow)
                .CopyModel(draft);

            var result = await table.InsertAsync(entity);
            if (!result)
                return draft.AsFailedResult<Note>("InsertAsync failed");

            return entity
                .ToListModel()
                .AsOkResult();
        }

        public async Task<Result> EditDraft(Note draft)
        {
            var table = GetTable(NoteEntity.TableName);

            // Assume the note is not published
            var draftKey = NoteEntity.GetDefaultKey(false);
            var entity = await table.RetrieveAsync<NoteEntity>(draftKey, draft.Id);
            
            if (entity == null)
                return draft.Id.AsFailedResult("NotFound");

            entity.CopyModel(draft);

            var result = await table.ReplaceAsync(entity);
            if (!result)
                return draft.AsFailedResult<Note>("ReplaceAsync failed");

            return entity
                .ToListModel()
                .AsOkResult();
        }

        public async Task<Result> PublishDraft(string id)
        {
            var table = GetTable(NoteEntity.TableName);

            var draftKey = NoteEntity.GetDefaultKey(false);
            var draftEntity = await table.RetrieveAsync<NoteEntity>(draftKey, id);

            if (draftEntity == null)
                return id.AsFailedResult("NotFound");

            var publishedEntity = NoteEntity
                .New(true, DateTime.UtcNow)
                .CopyModel(draftEntity.ToModel());

            var result = await table.InsertAsync(publishedEntity);
            if (!result)
                return id.AsFailedResult("InsertAsync failed");

            await table.DeleteAsync(draftEntity);
            return Result.Ok;
        }

        public async Task<Result> DeleteDraft(string id)
        {
            var table = GetTable(NoteEntity.TableName);

            var draftKey = NoteEntity.GetDefaultKey(false);
            var draftEntity = await table.RetrieveAsync<NoteEntity>(draftKey, id);

            if (draftEntity == null)
                return id.AsFailedResult("NotFound");

            var result = await table.DeleteAsync(draftEntity);
            if (!result)
                return id.AsFailedResult("DeleteAsync failed");
                
            return Result.Ok;
        }
        
        private CloudTable GetTable(string tableName)
        {
            var storageAccount = CloudStorageAccount.Parse(Settings.StorageConnectionString);
            var tableClient = storageAccount.CreateCloudTableClient();
            
            return tableClient.GetTableReference(tableName);
        }
        
        // Azure Functions

        [FunctionName("DraftsGetAll")]
        public static async Task<IActionResult> GetDraftsFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "drafts")] HttpRequest req, ILogger log)
        {                
            log.LogInformation($"C# Http trigger function executed: {nameof(GetDraftsFunction)}");

            return await HttpRunner.For<DraftsFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.GetDrafts());
        }

        [FunctionName("DraftsGet")]
        public static async Task<IActionResult> GetDraftFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "drafts/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(GetDraftFunction)}");

            return await HttpRunner.For<DraftsFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.GetDraft(id));
        }

        [FunctionName("DraftsCreate")]
        public static async Task<IActionResult> CreateDraftFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "drafts")] HttpRequest req, ILogger log)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(CreateDraftFunction)}");

            var note = await req.GetJsonBodyAsAsync<Note>();
            if (note == null)
                return new BadRequestResult();

            return await HttpRunner.For<DraftsFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.CreateDraft(note));
        }

        [FunctionName("DraftsEdit")]
        public static async Task<IActionResult> EditDraftFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "drafts/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(EditDraftFunction)}");

            var note = await req.GetJsonBodyAsAsync<Note>();
            if (note == null)
                return new BadRequestResult();

            note.Id = id;

            return await HttpRunner.For<DraftsFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.EditDraft(note));
        }

        [FunctionName("DraftsDelete")]
        public static async Task<IActionResult> DeleteDraftFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "drafts/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(DeleteDraftFunction)}");

            return await HttpRunner.For<DraftsFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.DeleteDraft(id));
        }

        [FunctionName("DraftsPublish")]
        public static async Task<IActionResult> PublishDraftFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "publish/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(PublishDraftFunction)}");

            return await HttpRunner.For<DraftsFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.PublishDraft(id));
        }
    }
}
