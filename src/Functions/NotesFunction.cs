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

        private CloudTable _table = null;
        private CloudTable Table
        {
            get
            {
                if (_table == null)
                {
                    var storageAccount = CloudStorageAccount.Parse(Settings.StorageConnectionString);
                    var tableClient = storageAccount.CreateCloudTableClient();

                    _table = tableClient.GetTableReference(NoteEntity.TableName);
                }

                return _table;
            }
        }

        public async Task<IReadOnlyCollection<Note>> GetNotes(bool published)
        {
            var key = NoteEntity.GetDefaultKey(published);
            var entities = await Table.ScanAsync<NoteEntity>(key);
            
            return entities
                .Select(n => n.ToListModel())
                .ToList()
                .AsReadOnly();
        }

        public async Task<Note> GetNote(string id)
        {
            var entity = await GetDraft(id);
            return entity?.ToModel();
        }

        public async Task<Result> CreateNote(Note note)
        {
            var entity = NoteEntity
                .New(false, DateTime.UtcNow)
                .CopyModel(note);

            var result = await Table.InsertAsync(entity);
            if (!result)
                return note.AsFailedResult<Note>("InsertAsync failed");

            return entity
                .ToListModel()
                .AsOkResult();
        }

        public async Task<Result> EditNote(Note note)
        {
            var entity = await GetDraft(note.Id);
            if (entity == null)
                return note.Id.AsFailedResult("NotFound");

            entity.CopyModel(note);

            var result = await Table.ReplaceAsync(entity);
            if (!result)
                return note.AsFailedResult<Note>("ReplaceAsync failed");

            return entity
                .ToListModel()
                .AsOkResult();
        }

        public async Task<Result> DeleteNote(string id)
        {
            var entity = await GetDraft(id);
            if (entity == null)
                return id.AsFailedResult("NotFound");

            var result = await Table.DeleteAsync(entity);
            if (!result)
                return id.AsFailedResult("DeleteAsync failed");
                
            return Result.Ok;
        }
                
        private async Task<NoteEntity> GetDraft(string id)
        {
            var draftKey = NoteEntity.GetDefaultKey(false);
            return await Table.RetrieveAsync<NoteEntity>(draftKey, id);
        }
        
        // Azure Functions

        [FunctionName("NotesGetAll")]
        public static async Task<IActionResult> GetNotesFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes")] HttpRequest req, ILogger log)
        {                
            log.LogInformation($"C# Http trigger function executed: {nameof(GetNotesFunction)}");

            var query = req.GetQueryParameterDictionary();
            var published = query.TryGetValue("published", out var value) && bool.TryParse(value, out var boolValue) ?
                boolValue : false;

            return await HttpRunner.For<NotesFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.GetNotes(published));
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

        [FunctionName("NotesCreate")]
        public static async Task<IActionResult> CreateNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "notes")] HttpRequest req, ILogger log)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(CreateNoteFunction)}");

            var note = await req.GetJsonBodyAsAsync<Note>();
            if (note == null)
                return new BadRequestResult();

            return await HttpRunner.For<NotesFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.CreateNote(note));
        }

        [FunctionName("NotesEdit")]
        public static async Task<IActionResult> EditNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "notes/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(EditNoteFunction)}");

            var note = await req.GetJsonBodyAsAsync<Note>();
            if (note == null)
                return new BadRequestResult();

            note.Id = id;

            return await HttpRunner.For<NotesFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.EditNote(note));
        }

        [FunctionName("NotesDelete")]
        public static async Task<IActionResult> DeleteNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "notes/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(DeleteNoteFunction)}");

            return await HttpRunner.For<NotesFunction>()
                .WithAuthorizedRequest(req)
                .WithLogger(log)
                .ExecuteAsync(f => f.DeleteNote(id));
        }
    }
}
