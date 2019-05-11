using System;

namespace Alejof.Notes.Models
{
    public class Content
    {
        public string Type { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Date { get; set; }
        public string Text { get; set; }
        public string SourceUrl { get; set; }
        public string SourceName { get; set; }
    }
}
