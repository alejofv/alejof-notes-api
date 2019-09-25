using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alejof.Notes.Extensions;
using Alejof.Notes.Auth;
using Alejof.Notes.Infrastructure;
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

        private CloudTable _noteTable = null;
        private CloudTable _dataTable = null;
        private CloudTable NoteTable => _noteTable = _noteTable ?? Settings.StorageConnectionString.GetTable(NoteEntity.TableName);
        private CloudTable DataTable => _dataTable = _dataTable ?? Settings.StorageConnectionString.GetTable(DataEntity.TableName);

        public async Task<Result> Publish(string id, bool publish)
        {
            var oldKey = NoteEntity.GetKey(this.AuthContext.TenantId, !publish);
            var oldEntity = await NoteTable.RetrieveAsync<NoteEntity>(oldKey, id);

            if (oldEntity == null)
                return id.AsFailedResult("NotFound");

            var newEntity = NoteEntity
                .New(this.AuthContext.TenantId, publish, DateTime.UtcNow)
                .CopyFrom(oldEntity);

            var result = await NoteTable.InsertAsync(newEntity);
            if (!result)
                return id.AsFailedResult("InsertAsync failed");

            await MoveData(oldKey, newEntity.PartitionKey, newEntity.Uid);
            await NoteTable.DeleteAsync(oldEntity);

            return Result.Ok;
        }
        
        private async Task<Result> MoveData(string oldKey, string newKey, string noteUid)
        {            
            var oldData = await DataTable.QueryAsync<DataEntity>(oldKey, FilterBy.RowKey.Like(noteUid));
            if (oldData.Any())
            {
                var newData = oldData
                    .Select(d => new DataEntity { PartitionKey = newKey }.CopyFrom(d))
                    .ToList();

                var insertBatch = new TableBatchOperation();
                newData.ForEach(d => insertBatch.Insert(d));

                var deleteBatch = new TableBatchOperation();
                oldData.ForEach(d => deleteBatch.Delete(d));

                await Task.WhenAll(DataTable.ExecuteBatchAsync(insertBatch), DataTable.ExecuteBatchAsync(insertBatch));
            }

            return Result.Ok;
        }
    }
}
