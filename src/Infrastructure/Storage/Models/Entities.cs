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
        
        public string Title { get; set; }
        public string Slug { get; set; }
        public string BlobUri { get; set; }
        public string Uid { get; set; }

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
        
        public string Value { get; set; }

        private string _noteId = null;
        public string NoteId => _noteId = _noteId ?? RowKey.Split('_')[0];
        
        private string _name = null;
        public string Name => _name = _name ?? RowKey.Split('_')[1];
    }

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
