#nullable enable

using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Storage
{
    ///
    /// <summary>PartitionKey: "tenantId_(draft|published)". RowKey: ReverseDate</summary>
    ///
    public class NoteEntity : TableEntity
    {
        public const string TableName = "NoteAppEntries";
        
        public static string GetKey(string tenantId, bool published) => $"{tenantId}_{(published ? "published" : "draft")}";
        private static readonly DateTime RefDate = new DateTime(2100, 1, 1);
        
        public string? Title { get; set; }
        public string? Slug { get; set; }
        public string? BlobUri { get; set; }
        public string? Uid { get; set; }

        public DateTime Date => RefDate - TimeSpan.FromSeconds(double.Parse(RowKey));

        public static NoteEntity New(string tenantId, bool published, DateTime date) =>
            new NoteEntity
            {
                PartitionKey = GetKey(tenantId, published),
                RowKey = (RefDate - date).TotalSeconds.ToString("F0"),
                Uid = Guid.NewGuid().ToString(),
            };
    }

    ///
    /// <summary>PartitionKey: "tenantId_(draft|published)". RowKey: noteGuid_Name</summary>
    ///
    public class DataEntity : TableEntity
    {
        public const string TableName = "NoteAppEntryData";
        
        public string? Value { get; set; }

        public string NoteId => RowKey.Split('_')[0];
        public string Name => RowKey.Split('_')[1];
    }

    ///
    /// <summary>PartitionKey: "tenantId". RowKey: GUID</summary>
    ///
    public class MediaEntity : TableEntity
    {
        public const string TableName = "NoteAppMedia";
        
        public string? Name { get; set; }
        public string? BlobUri { get; set; }

        public static MediaEntity New(string tenantId)
        {
            return new MediaEntity
            {
                PartitionKey = tenantId,
                RowKey = Guid.NewGuid().ToString(),
            };
        }
    }

    ///
    /// <summary>PartitionKey: "tenantId". RowKey: reverse ticks</summary>
    ///
    public class AuditLogEntity : TableEntity
    {
        public const string TableName = "NoteAppLogs";

        public string? Email { get; set; }
        public string? Action { get; set; }
        public string? Message { get; set; }
        
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
