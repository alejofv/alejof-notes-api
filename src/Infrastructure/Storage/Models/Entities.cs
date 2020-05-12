#nullable enable

using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Storage
{
    /// <summary>
    /// PartitionKey: "tenantId". RowKey: GUID
    /// </summary>
    public class NoteEntity : TableEntity
    {
        public const string TableName = "NoteAppEntries";
        
        public string? Title { get; set; }
        public string? Slug { get; set; }
        public string? BlobUri { get; set; }
        public bool IsPublished { get; set; }
        public string? PublishedBlobUri { get; set; }
    }

    /// <summary>
    /// PartitionKey: "tenantId". RowKey: noteGuid-Name
    /// </summary>
    public class DataEntity : TableEntity
    {
        public const string TableName = "NoteAppEntryData";
        
        public string? Value { get; set; }

        public string NoteId => RowKey.Split('_')[0];
        public string Name => RowKey.Split('_')[1];
    }

    /// <summary>
    /// PartitionKey: "tenantId". RowKey: GUID
    /// </summary>
    public class MediaEntity : TableEntity
    {
        public const string TableName = "NoteAppMedia";
        
        public string? Name { get; set; }
        public string? BlobUri { get; set; }
    }

    /// <summary>
    /// PartitionKey: "tenantId". RowKey: reverse ticks
    /// </summary>
    public class AuditLogEntity : TableEntity
    {
        public const string TableName = "NoteAppLogs";

        public string? Action { get; set; }
        public string? Request { get; set; }
        public string? Response { get; set; }
        
        public static AuditLogEntity New(string tenantId)
        {
            return new AuditLogEntity
            {
                PartitionKey = tenantId,
                RowKey = string.Format("{0:D19}", DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks),
            };
        }
    }
}
