using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Alejof.Notes.Functions.TableStorage;
using Humanizer;

namespace Alejof.Notes.Functions.Mapping
{
    public static class NotesMapper
    {
        public static Models.Note MapToModel(this NoteEntity entity, IEnumerable<DataEntity> dataEntities = null) =>
            new Models.Note
            {
                Id = entity.RowKey,
                Title = entity.Title,
                Slug = entity.Slug,
                DateText = entity.Date.Humanize(utcDate: true),
                Data = (dataEntities ?? Enumerable.Empty<DataEntity>())
                    .ToDictionary(
                        keySelector: d => d.Name,
                        elementSelector: d => d.Value),
            };
            
        public static NoteEntity MapFromModel(this NoteEntity entity, Models.Note note)
        {
            entity.Title = note.Title;
            entity.Slug = note.Slug;

            return entity;
        }

        public static List<DataEntity> MapDataFromModel(this NoteEntity entity, Models.Note note)
        {
            if (note.Data?.Any() != true)
                return Enumerable.Empty<DataEntity>().ToList();
                
            return note.Data
                .Select(
                    entry => new DataEntity
                    {
                        PartitionKey = entity.PartitionKey,
                        RowKey = $"{entity.Uid}_{entry.Key}",
                        Value = entry.Value,
                    })
                .ToList();
        }
    }
}
