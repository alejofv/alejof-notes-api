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
    public class NotesFunction : IFunction
    {
        public ILogger Log { get; set; }
        public FunctionSettings Settings { get; set; }

        public async Task<IReadOnlyCollection<Note>> GetNotes(bool published)
        {
            var table = GetTable(NoteEntity.TableName);

            var key = NoteEntity.GetDefaultKey(published);
            Log.LogInformation($"Getting notes from storage. TableName: {NoteEntity.TableName}, Key: {key}");

            var notes = await table.ScanAsync<NoteEntity>(key);
            return notes
                .Select(n => n.ToModel())
                .ToList()
                .AsReadOnly();
        }

        public async Task<Result> CreateDraft(Note note)
        {
            var table = GetTable(NoteEntity.TableName);

            var noteEntity = NoteEntity
                .New(false, DateTime.UtcNow)
                .CopyModel(note);

            var result = await table.InsertAsync(noteEntity);
            if (!result)
                return note.AsFailedResult<Note>("InsertAsync failed");

            return noteEntity
                .ToModel()
                .AsOkResult();
        }

        public async Task<Result> EditDraft(Note note)
        {
            var table = GetTable(NoteEntity.TableName);

            // Assume the note is not published
            var draftKey = NoteEntity.GetDefaultKey(false);
            var noteEntity = await table.RetrieveAsync<NoteEntity>(draftKey, note.Id);
            
            if (noteEntity == null)
                return note.Id.AsFailedResult("NotFound");

            noteEntity.CopyModel(note);

            var result = await table.ReplaceAsync(noteEntity);
            if (!result)
                return note.AsFailedResult<Note>("ReplaceAsync failed");

            return noteEntity
                .ToModel()
                .AsOkResult();
        }

        public async Task<Result> PublishDraft(string id)
        {
            var table = GetTable(NoteEntity.TableName);

            var draftKey = NoteEntity.GetDefaultKey(false);
            var noteEntity = await table.RetrieveAsync<NoteEntity>(draftKey, id);

            if (noteEntity == null)
                return id.AsFailedResult("NotFound");

            var publishedEntity = NoteEntity
                .New(true, DateTime.UtcNow)
                .CopyModel(noteEntity.ToModel());

            var result = await table.InsertAsync(publishedEntity);
            if (!result)
                return id.AsFailedResult("InsertAsync failed");

            await table.DeleteAsync(noteEntity);
            return Result.Ok;
        }
        
        private CloudTable GetTable(string tableName)
        {
            var storageAccount = CloudStorageAccount.Parse(Settings.StorageConnectionString);
            var tableClient = storageAccount.CreateCloudTableClient();
            
            return tableClient.GetTableReference(tableName);
        }
        
        // Azure Functions

        [FunctionName("NotesGet")]
        public static async Task<IActionResult> GetNotesFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes")] HttpRequest req, ILogger log)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(GetNotesFunction)}");

            return await HttpRunner.For<NotesFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.GetNotes(true));
        }
        
        [FunctionName("NotesGetDrafts")]
        public static async Task<IActionResult> GetDraftsFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes/drafts")] HttpRequest req, ILogger log)
        {                
            log.LogInformation($"C# Http trigger function executed: {nameof(GetDraftsFunction)}");

            return await HttpRunner.For<NotesFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.GetNotes(false));
        }

        [FunctionName("NotesCreateDraft")]
        public static async Task<IActionResult> CreateDraftFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "notes/drafts")] HttpRequest req, ILogger log)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(CreateDraftFunction)}");

            var note = await req.GetJsonBodyAsAsync<Note>();
            if (note == null)
                return new BadRequestResult();

            return await HttpRunner.For<NotesFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.CreateDraft(note));
        }

        [FunctionName("NotesEditDraft")]
        public static async Task<IActionResult> EditDraftFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "notes/drafts/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(EditDraftFunction)}");

            var note = await req.GetJsonBodyAsAsync<Note>();
            if (note == null)
                return new BadRequestResult();

            note.Id = id;

            return await HttpRunner.For<NotesFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.EditDraft(note));
        }
    }
}
