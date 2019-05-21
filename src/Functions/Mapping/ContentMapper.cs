using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Alejof.Notes.Functions.TableStorage;
using Humanizer;

namespace Alejof.Notes.Functions.Mapping
{
    public static class ContentMapper
    {
        public static Models.Content ToContentModel(this NoteEntity entity)
        {            
            return new Models.Content
            {
                Title = entity.Title,
                Slug = entity.Slug,
                Type = entity.Type,
                Date = entity.Date.ToString("yyyy-MM-dd"),
                SourceUrl = entity.Source,
                SourceName = entity.Source.UrlDomain(),
                ContentUrl = entity.BlobUri,
            };
        }
    }
}
