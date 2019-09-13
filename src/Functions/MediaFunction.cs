using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alejof.Notes.Extensions;
using Alejof.Notes.Infrastructure;
using Alejof.Notes.Functions.Mapping;
using Alejof.Notes.Functions.TableStorage;
using Alejof.Notes.Models;
using Alejof.Notes.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Alejof.Notes.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Alejof.Notes.Functions
{
    public class MediaFunction : IAuthorizedFunction
    {
        public const string BlobContainerName = "note-media";
        
        public AuthContext AuthContext { get; set; }
        public ILogger Log { get; set; }
        public FunctionSettings Settings { get; set; }

        private CloudTable _table = null;
        private CloudBlobContainer _container = null;
        
        private CloudTable Table => _table = _table ?? Settings.StorageConnectionString.GetTable(MediaEntity.TableName);
        private CloudBlobContainer Container => _container = _container ?? Settings.StorageConnectionString.GetBlobContainer(BlobContainerName);
        
        public async Task<IReadOnlyCollection<Media>> GetMedia()
        {
            var entities = await Table.ScanAsync<MediaEntity>(this.AuthContext.TenantId);

            return entities
                .Select(n => n.ToModel())
                .ToList()
                .AsReadOnly();
        }

        public string GetMediaPath(string name) => $"{BlobContainerName}/{GetMediaName(name)}";
        private string GetMediaName(string name) => $"{AuthContext.TenantId}/{name}";

        public async Task<Result> CreateMedia(string name)
        {
            var blob = Container.GetBlockBlobReference(GetMediaName(name));

            var entity = MediaEntity
                .New(this.AuthContext.TenantId);
                
            entity.Name = name;
            entity.BlobUri = blob.Uri.ToString();

            var result = await Table.InsertAsync(entity);
            if (!result)
                return "InsertAsync failed".AsFailedResult();

            return entity
                .ToModel()
                .AsOkResult();
        }

        public async Task<Result> DeleteMedia(string id)
        {
            var entity = await Table.RetrieveAsync<NoteEntity>(this.AuthContext.TenantId, id);
            if (entity == null)
                return id.AsFailedResult("NotFound");

            var result = await Table.DeleteAsync(entity);
            if (!result)
                return id.AsFailedResult("DeleteAsync failed");

            await Container.DeleteAsync(entity.BlobUri);

            return Result.Ok;
        }
    }
}
