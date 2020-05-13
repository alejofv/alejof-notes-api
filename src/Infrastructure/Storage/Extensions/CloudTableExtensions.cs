#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Storage
{
    public static class CloudTableExtensions
    {   
        public static async Task<TEntity?> RetrieveAsync<TEntity>(this CloudTable table, string partitionKey, string rowKey)
            where TEntity : TableEntity, new()
        {
            await table.CreateIfNotExistsAsync();

            var retrieveOperation = TableOperation.Retrieve<TEntity>(partitionKey, rowKey);
            var result = await table.ExecuteAsync(retrieveOperation);

            return result.Result as TEntity;
        }

        public static async Task<List<TEntity>> ScanAsync<TEntity>(this CloudTable table, string partitionKey)
            where TEntity : TableEntity, new()
        {
            await table.CreateIfNotExistsAsync();

            var query = new TableQuery<TEntity>()
                .Where($"PartitionKey eq '{partitionKey}'");
                
            var segment = await table.ExecuteQuerySegmentedAsync(query, null);
            return segment.ToList();
        }

        public static async Task<List<TEntity>> QueryAsync<TEntity>(this CloudTable table, string partitionKey, string? filter)
            where TEntity : TableEntity, new()
        {
            await table.CreateIfNotExistsAsync();

            if (string.IsNullOrWhiteSpace(filter))
                return await table.ScanAsync<TEntity>(partitionKey);

            var query = new TableQuery<TEntity>()
                .Where($"PartitionKey eq '{partitionKey}' and {filter}");

            var segment = await table.ExecuteQuerySegmentedAsync(query, null);
            return segment.ToList();
        }

        public static async Task<bool> InsertAsync(this CloudTable table, ITableEntity entity)
        {
            await table.CreateIfNotExistsAsync();

            var operation = TableOperation.Insert(entity);
            var result = await table.ExecuteAsync(operation);

            return result.IsSuccess();
        }

        public static async Task<bool> ReplaceAsync(this CloudTable table, ITableEntity entity, bool insertIfNotFound = false)
        {
            await table.CreateIfNotExistsAsync();

            var operation = insertIfNotFound ?
                TableOperation.InsertOrReplace(entity)
                : TableOperation.Replace(entity);

            var result = await table.ExecuteAsync(operation);

            return result.IsSuccess();
        }

        public static async Task<bool> DeleteAsync(this CloudTable table, ITableEntity entity)
        {
            await table.CreateIfNotExistsAsync();
            
            var operation = TableOperation.Delete(entity);
            var result = await table.ExecuteAsync(operation);

            return result.IsSuccess();
        }

        public static bool IsSuccess(this TableResult result) => result.HttpStatusCode >= 200 && result.HttpStatusCode < 300;
    }
    
    public static class FilterBy
    {
        public const string RowKey = "RowKey";

        private const char NumRangeStart = (char)0x30;
        private const char NumRangeEnd = (char)0x3A;

        private const char RangeStart = (char)0x20;
        private const char RangeEnd = (char)0x7F;

        public static string Like(this string field, string? value) => $"{field} gt '{value}{RangeStart}' and {field} lt '{value}{RangeEnd}'";
        public static string InRange(this string field, string? value) => $"{field} ge '{value}{NumRangeStart}' and {field} lt '{value}{NumRangeEnd}'";
    }
}
