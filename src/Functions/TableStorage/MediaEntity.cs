using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Functions.TableStorage
{
    ///
    /// <summary>PartitionKey: "tenantId". RowKey: GUID</summary>
    ///
    public class MediaEntity : TableEntity
    {
        public const string TableName = "NoteAppMedia";
        
        public string Name { get; set; }
        public string BlobUri { get; set; }

        public static MediaEntity New(string tenantId)
        {
            return new MediaEntity
            {
                PartitionKey = tenantId,
                RowKey = Guid.NewGuid().ToString(),
            };
        }
    }
}
