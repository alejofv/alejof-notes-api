using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Functions.TableStorage
{
    ///
    /// <summary>PartitionKey: "tenantId_(draft|published)". RowKey: ReverseDate</summary>
    ///
    public class NoteEntity : TableEntity
    {
        public const string TableName = "AlejoFNotes";
        
        public static string GetKey(string tenantId, bool published) => $"{tenantId}_{(published ? "published" : "draft")}";
        private static readonly DateTime RefDate = new DateTime(2100, 1, 1);
        
        public string Type { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Source { get; set; }
        public string BlobUri { get; set; }

        public DateTime Date => RefDate - TimeSpan.FromSeconds(double.Parse(RowKey));
        public string FileName => $"{Date.ToString("yyyy-MM-dd")}-{Slug}";

        public static NoteEntity New(string tenantId, bool published, DateTime date)
        {
            return new NoteEntity
            {
                PartitionKey = GetKey(tenantId, published),
                RowKey = (RefDate - date).TotalSeconds.ToString("F0"),
            };
        }
    }
}
