using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Extensions
{
    public static class CloudTableExtensions
    {
        public static async Task<TEntity> RetrieveAsync<TEntity>(this CloudTable table, string partitionKey, string rowKey)
            where TEntity : TableEntity, new()
        {
            var retrieveOperation = TableOperation.Retrieve<TEntity>(partitionKey, rowKey);
            var result = await table.ExecuteAsync(retrieveOperation);

            return result.Result as TEntity;
        }

        public static async Task<List<TEntity>> ScanAsync<TEntity>(this CloudTable table, string partitionKey)
            where TEntity : TableEntity, new()
        {
            var query = new TableQuery<TEntity>()
                .Where($"PartitionKey eq '{partitionKey}'");
                
            var segment = await table.ExecuteQuerySegmentedAsync(query, null);
            return segment.ToList();
        }

        public static async Task<bool> InsertAsync(this CloudTable table, ITableEntity entity)
        {
            var operation = TableOperation.Insert(entity);
            var result = await table.ExecuteAsync(operation);

            return result.IsSuccess();
        }

        public static async Task<bool> ReplaceAsync(this CloudTable table, ITableEntity entity, bool insertIfNotFound = false)
        {
            var operation = insertIfNotFound ?
                TableOperation.InsertOrReplace(entity)
                : TableOperation.Replace(entity);

            var result = await table.ExecuteAsync(operation);

            return result.IsSuccess();
        }

        public static async Task<bool> MergeAsync(this CloudTable table, ITableEntity entity, bool insertIfNotFound = false)
        {
            var operation = insertIfNotFound ?
                TableOperation.InsertOrMerge(entity)
                : TableOperation.Merge(entity);

            var result = await table.ExecuteAsync(operation);

            return result.IsSuccess();
        }

        public static async Task<bool> DeleteAsync(this CloudTable table, ITableEntity entity)
        {
            var operation = TableOperation.Delete(entity);
            var result = await table.ExecuteAsync(operation);

            return result.IsSuccess();
        }

        public static bool IsSuccess(this TableResult result) => result.HttpStatusCode >= 200 && result.HttpStatusCode < 300;
    }
}
