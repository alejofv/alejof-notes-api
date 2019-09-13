using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alejof.Notes.Extensions;
using Alejof.Notes.Functions.Auth;
using Alejof.Notes.Functions.Infrastructure;
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

        private CloudTable _table = null;
        private CloudBlobContainer _container = null;
        
        private CloudTable Table => _table = _table ?? Settings.StorageConnectionString.GetTable(NoteEntity.TableName);
        private CloudBlobContainer Container => _container = _container ?? Settings.StorageConnectionString.GetBlobContainer(BlobContainerName);

        public async Task<IReadOnlyCollection<Note>> GetNotes(bool published, bool preserveFullSources)
        {
            var key = NoteEntity.GetKey(this.AuthContext.TenantId, published);
            var entities = await Table.ScanAsync<NoteEntity>(key);

            return entities
                .Select(n => n.ToListModel(shortenSourceLinks: !preserveFullSources))
                .ToList()
                .AsReadOnly();
        }

        public async Task<Note> GetNote(string id)
        {
            var entity = await GetNoteEntity(id);
            var model = entity?.ToModel();
            
            // Get content from blob
            if (!string.IsNullOrEmpty(entity.BlobUri))
                model.Content = await DownloadContent(entity.BlobUri);

            return model;
        }

        public async Task<Result> CreateNote(Note note, string format)
        {
            var entity = NoteEntity
                .New(this.AuthContext.TenantId, false, DateTime.UtcNow)
                .CopyModel(note);

            var filename = GetNoteFilename(entity, format);
            entity.BlobUri = await UploadContent(note.Content, filename);

            var result = await Table.InsertAsync(entity);
            if (!result)
                return note.AsFailedResult("InsertAsync failed");

            return entity
                .ToModel()
                .AsOkResult();
        }

        public async Task<Result> EditNote(Note note, string format)
        {
            var entity = await GetNoteEntity(note.Id);
            if (entity == null)
                return note.Id.AsFailedResult("NotFound");

            var previousUri = entity.BlobUri;
            var filename = GetNoteFilename(entity, format);

            entity.CopyModel(note);
            entity.BlobUri = await UploadContent(note.Content, filename);
            
             // Delete old content (after uploading the new one)
             if (!string.Equals(entity.BlobUri, previousUri, StringComparison.OrdinalIgnoreCase))
                await DeleteContent(previousUri);

            var result = await Table.ReplaceAsync(entity);
            if (!result)
                return note.AsFailedResult("ReplaceAsync failed");

            return entity
                .ToModel()
                .AsOkResult();
        }

        public async Task<Result> DeleteNote(string id)
        {
            var entity = await GetNoteEntity(id);
            if (entity == null)
                return id.AsFailedResult("NotFound");

            var result = await Table.DeleteAsync(entity);
            if (!result)
                return id.AsFailedResult("DeleteAsync failed");

            await DeleteContent(entity.BlobUri);
                
            return Result.Ok;
        }
                
        private async Task<NoteEntity> GetNoteEntity(string id)
        {
            var draftKey = NoteEntity.GetKey(this.AuthContext.TenantId, false);
            return await Table.RetrieveAsync<NoteEntity>(draftKey, id);
        }
        
        private string GetNoteFilename(NoteEntity entity, string format)
        {
            return $"{entity.Date.ToString("yyyy-MM-dd")}-{entity.Slug}.{format}";
        }
        
        private async Task<string> UploadContent(string content, string filename)
        {
            var blob = Container.GetBlockBlobReference(filename.ToLowerInvariant());
            using (var data = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            {
                await blob.UploadFromStreamAsync(data);
            }

            return blob.Uri.ToString();
        }

        private async Task<string> DownloadContent(string uri)
        {
            var blob = await Container.ServiceClient.GetBlobReferenceFromServerAsync(new Uri(uri));

            using (var sm = new MemoryStream())
            {
                await blob.DownloadToStreamAsync(sm);
                return Encoding.UTF8.GetString(sm.ToArray());
            }
        }

        private async Task DeleteContent(string uri)
        {
            var blob = await Container.ServiceClient.GetBlobReferenceFromServerAsync(new Uri(uri));
            await blob.DeleteIfExistsAsync();
        }
    }
}
