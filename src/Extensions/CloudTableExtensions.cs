using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Functions.Extensions
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
    }
}
