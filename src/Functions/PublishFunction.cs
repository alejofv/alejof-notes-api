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
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Functions
{
    public class PublishFunction : IAuthorizedFunction
    {
        public AuthContext AuthContext { get; set; }
        public ILogger Log { get; set; }
        public FunctionSettings Settings { get; set; }

        private CloudTable _table = null;
        private CloudTable Table => _table = _table ?? Settings.StorageConnectionString.GetTable(NoteEntity.TableName);

        public async Task<Result> Publish(string id, bool publish)
        {
            var existingKey = NoteEntity.GetKey(this.AuthContext.TenantId, !publish);
            var existingEntity = await Table.RetrieveAsync<NoteEntity>(existingKey, id);

            if (existingEntity == null)
                return id.AsFailedResult("NotFound");

            var newEntity = NoteEntity
                .New(this.AuthContext.TenantId, publish, DateTime.UtcNow)
                .CopyModel(existingEntity.ToModel());
            newEntity.BlobUri = existingEntity.BlobUri;

            var result = await Table.InsertAsync(newEntity);
            if (!result)
                return id.AsFailedResult("InsertAsync failed");

            await Table.DeleteAsync(existingEntity);
            return Result.Ok;
        }
    }
}
