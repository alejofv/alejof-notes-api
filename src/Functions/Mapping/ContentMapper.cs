using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Alejof.Notes.Functions.TableStorage;
using Humanizer;

namespace Alejof.Notes.Functions.Mapping
{
    public static class ContentMapper
    {
        private const string SourceDataName = "Source";
        private const string SourceNameDataName = "SourceName";
        
        public static Models.Content MapToContentModel(this NoteEntity entity, IEnumerable<DataEntity> dataEntities)
        {
            var data = (dataEntities ?? Enumerable.Empty<DataEntity>())
                    .ToDictionary(
                        keySelector: d => d.Name,
                        elementSelector: d => d.Value);
                        
            // Specific data name mappings
            if (data.ContainsKey(SourceDataName))
                data.Add(SourceNameDataName, data[SourceDataName].UrlDomain());
            
            return new Models.Content
            {
                Title = entity.Title,
                Slug = entity.Slug,
                ContentUrl = entity.BlobUri,
                Date = entity.Date.ToString("yyyy-MM-dd"),
                Data = data,
            };
        }
    }
}
