using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Functions.TableStorage
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
        public string Guid { get; set; }

        [Obsolete("Use NoteData instead")] public string Type { get; set; }
        [Obsolete("Use NoteData instead")] public string Source { get; set; }
        [Obsolete("Use NoteData instead")] public string HeaderUri { get; set; }

        public DateTime Date => RefDate - TimeSpan.FromSeconds(double.Parse(RowKey));

        public static NoteEntity New(string tenantId, bool published, DateTime date)
        {
            return new NoteEntity
            {
                PartitionKey = GetKey(tenantId, published),
                RowKey = (RefDate - date).TotalSeconds.ToString("F0"),
            };
        }
    }

    ///
    /// <summary>PartitionKey: Note Guid. RowKey: Data Name</summary>
    ///
    public class NoteDataEntity : TableEntity
    {
        public const string TableName = "NoteAppEntryData";

        public string Value { get; set; }
    }
}
