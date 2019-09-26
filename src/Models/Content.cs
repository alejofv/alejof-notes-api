using System;
using System.Collections.Generic;

namespace Alejof.Notes.Models
{
    public class Content
    {
        public string Type { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string ContentUrl { get; set; }
        public string Date { get; set; }

        public IDictionary<string, string> Data { get; set; }
    }
}
