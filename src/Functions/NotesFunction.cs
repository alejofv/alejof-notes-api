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

        public async Task<IReadOnlyCollection<Note>> GetNotes()
        {
            var table = GetTable(NoteEntity.TableName);

            var noteKey = NoteEntity.GetDefaultKey(true);
            Log.LogInformation($"Getting notes from storage. TableName: {NoteEntity.TableName}, Key: {noteKey}");

            var notes = await table.ScanAsync<NoteEntity>(noteKey);
            return notes
                .Select(n => n.ToListModel())
                .ToList()
                .AsReadOnly();
        }

        public async Task<Note> GetNote(string id)
        {
            var table = GetTable(NoteEntity.TableName);

            var noteKey = NoteEntity.GetDefaultKey(true);
            var note = await table.RetrieveAsync<NoteEntity>(noteKey, id);

            return note?.ToModel();
        }

        public async Task<Result> UnpublishNote(string id)
        {
            var table = GetTable(NoteEntity.TableName);

            var noteKey = NoteEntity.GetDefaultKey(true);
            var noteEntity = await table.RetrieveAsync<NoteEntity>(noteKey, id);

            if (noteEntity == null)
                return id.AsFailedResult("NotFound");

            var draftEntity = NoteEntity
                .New(false, DateTime.UtcNow)
                .CopyModel(noteEntity.ToModel());

            var result = await table.InsertAsync(draftEntity);
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

        [FunctionName("NotesGetAll")]
        public static async Task<IActionResult> GetNotesFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes")] HttpRequest req, ILogger log)
        {                
            log.LogInformation($"C# Http trigger function executed: {nameof(GetNotesFunction)}");

            return await HttpRunner.For<NotesFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.GetNotes());
        }

        [FunctionName("NotesGet")]
        public static async Task<IActionResult> GetNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(GetNoteFunction)}");

            return await HttpRunner.For<NotesFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.GetNote(id));
        }

        [FunctionName("NotesUnpublish")]
        public static async Task<IActionResult> UnpublishNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "unpublish/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(UnpublishNoteFunction)}");

            return await HttpRunner.For<NotesFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.UnpublishNote(id));
        }
    }
}
