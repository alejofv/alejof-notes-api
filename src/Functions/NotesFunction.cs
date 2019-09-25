using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alejof.Notes.Extensions;
using Alejof.Notes.Auth;
using Alejof.Notes.Infrastructure;
using Alejof.Notes.Functions.Mapping;
using Alejof.Notes.Functions.TableStorage;
using Alejof.Notes.Models;
using Alejof.Notes.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Functions
{
    public class NotesFunction : IAuthorizedFunction
    {
        public const string BlobContainerName = "note-entries";
        
        public AuthContext AuthContext { get; set; }
        public ILogger Log { get; set; }
        public FunctionSettings Settings { get; set; }

        private CloudTable _noteTable = null;
        private CloudTable _dataTable = null;
        private CloudBlobContainer _container = null;
        
        private CloudTable NoteTable => _noteTable = _noteTable ?? Settings.StorageConnectionString.GetTable(NoteEntity.TableName);
        private CloudTable DataTable => _dataTable = _dataTable ?? Settings.StorageConnectionString.GetTable(DataEntity.TableName);
        private CloudBlobContainer Container => _container = _container ?? Settings.StorageConnectionString.GetBlobContainer(BlobContainerName);

        public async Task<IReadOnlyCollection<Note>> GetNotes(bool published)
        {
            var key = NoteEntity.GetKey(this.AuthContext.TenantId, published);
            
            var notesTask = NoteTable.ScanAsync<NoteEntity>(key);
            var dataTask = DataTable.ScanAsync<DataEntity>(key);

            await Task.WhenAll(notesTask, dataTask);

            return notesTask.Result
                .Select(
                    n =>
                    {
                        var data = dataTask.Result
                            .Where(d => d.NoteId == n.Uid);

                        return n.MapToModel(data);
                    })
                .ToList()
                .AsReadOnly();
        }

        public async Task<Note> GetNote(string id, bool published)
        {
            var (entity, data) = await GetNoteEntities(id, published);
            var model = entity?.MapToModel(data);
            
            // Get content from blob
            if (!string.IsNullOrEmpty(entity.BlobUri))
                model.Content = await Container.DownloadAsync(entity.BlobUri);

            return model;
        }

        public async Task<Result> CreateNote(Note note, string format)
        {
            var entity = NoteEntity
                .New(this.AuthContext.TenantId, false, DateTime.UtcNow)
                .MapFromModel(note);

            var filename = GetNoteFilename(entity, format);
            entity.BlobUri = await Container.UploadAsync(note.Content, filename);

            var result = await NoteTable.InsertAsync(entity);
            if (!result)
                return note.AsFailedResult("InsertAsync failed");

            // Create and insert data
            var data = entity.MapDataFromModel(note);
            if (data.Any())
            {
                var batch = new TableBatchOperation();
                data.ForEach(d => batch.Insert(d));

                await DataTable.ExecuteBatchAsync(batch);
            }

            return entity
                .MapToModel(data)
                .AsOkResult();
        }

        public async Task<Result> EditNote(Note note, string format, bool published, bool updateContent = true)
        {
            var (entity, oldData) = await GetNoteEntities(note.Id, published);
            if (entity == null)
                return note.Id.AsFailedResult("NotFound");

            entity.MapFromModel(note);

            // Fix legacy notes
            if (string.IsNullOrEmpty(entity.Uid))
                entity.Uid = Guid.NewGuid().ToString();

            string oldContentUri = null;
            if (updateContent)
            {
                var filename = GetNoteFilename(entity, format);
                oldContentUri = entity.BlobUri;
                entity.BlobUri = await Container.UploadAsync(note.Content, filename);

                if (string.Equals(entity.BlobUri, oldContentUri, StringComparison.OrdinalIgnoreCase))
                    oldContentUri = string.Empty;
            }

            var result = await NoteTable.ReplaceAsync(entity);
            if (!result)
                return note.AsFailedResult("ReplaceAsync failed");

            // Update data
            var newData = entity.MapDataFromModel(note);
            if (oldData.Any() || newData.Any())
            {
                var batch = new TableBatchOperation();
                
                newData.ForEach(d => batch.InsertOrReplace(d));

                var dataNames = newData.Select(d => d.Name).ToArray();
                oldData
                    .Where(d => !dataNames.Contains(d.Name))
                    .ToList()
                    .ForEach(d => batch.Delete(d));

                await DataTable.ExecuteBatchAsync(batch);
            }
            
            if (!string.IsNullOrEmpty(oldContentUri))
                await Container.DeleteAsync(oldContentUri);

            return entity
                .MapToModel(newData)
                .AsOkResult();
        }

        public async Task<Result> DeleteNote(string id)
        {
            var (entity, data) = await GetNoteEntities(id, false);
            if (entity == null)
                return id.AsFailedResult("NotFound");

            var result = await NoteTable.DeleteAsync(entity);
            if (!result)
                return id.AsFailedResult("DeleteAsync failed");

            if (data.Any())
            {
                var batch = new TableBatchOperation();
                data.ForEach(d => batch.Delete(d));

                await DataTable.ExecuteBatchAsync(batch);
            }

            await Container.DeleteAsync(entity.BlobUri);
                
            return Result.Ok;
        }

        private async Task<(NoteEntity, List<DataEntity>)> GetNoteEntities(string id, bool published)
        {
            var note = await NoteTable.RetrieveAsync<NoteEntity>(NoteEntity.GetKey(this.AuthContext.TenantId, published), id);
            var data = await DataTable.QueryAsync<DataEntity>(note?.PartitionKey, FilterBy.RowKey.Like(note?.Uid));

            return (note, data);
        }

        private string GetNoteFilename(NoteEntity entity, string format) => $"{AuthContext.TenantId}/{entity.Date.ToString("yyyy-MM-dd")}-{entity.Slug}.{format}";
    }
}
