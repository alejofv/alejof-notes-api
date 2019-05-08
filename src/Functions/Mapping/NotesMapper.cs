using System;
using Alejof.Notes.Functions.TableStorage;
using Humanizer;

namespace Alejof.Notes.Functions.Mapping
{
    public static class NotesMapper
    {
        public static Models.Note ToModel(this NoteEntity entity) =>
            new Models.Note
            {
                Id = entity.RowKey,
                Type = entity.Type,
                Title = entity.Title,
                Slug = entity.Slug,
                Content = entity.Content,
                Source = entity.Source,
                Date = entity.Date.ToString("yyyy-MM-dd"),
                DateText = entity.Date.Humanize(utcDate: true),
            };
            
        public static NoteEntity CopyModel(this NoteEntity entity, Models.Note note)
        {
            entity.Title = note.Title;
            entity.Type = note.Type;
            entity.Slug = note.Slug;
            entity.Content = note.Content;
            entity.Source = note.Source;

            return entity;
        }
    }
}
