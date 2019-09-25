using System;
using System.Collections.Generic;

namespace Alejof.Notes.Models
{
    public class Note
    {
        public string Id { get; set; } // Reverse date
        public string DateText { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Content { get; set; }
        
        public IDictionary<string, string> Data { get; set; }

        [Obsolete("Use Data instead")] public string Type { get; set; }
        [Obsolete("Use Data instead")] public string Source { get; set; }
        [Obsolete("Use Data instead")] public string HeaderImageUrl { get; set; }
    }
}
