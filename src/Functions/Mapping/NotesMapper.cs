using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Alejof.Notes.Functions.TableStorage;
using Humanizer;

namespace Alejof.Notes.Functions.Mapping
{
    public static class NotesMapper
    {
        public static Models.Note ToListModel(this NoteEntity entity) =>
            new Models.Note
            {
                Id = entity.RowKey,
                Type = entity.Type,
                Title = entity.Title,
                Source = entity.Source.FirstUrl(),
                Date = entity.Date.ToString("yyyy-MM-dd"),
                DateText = entity.Date.Humanize(utcDate: true),
            };

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

    public static class MapperExtensions
    {
        private static readonly Regex LinkParser = new Regex(@"\b(?:https?:\/\/|www\.)([^ \f\n\r\t\v\]]+)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string FirstUrl(this string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                var match = LinkParser.Matches(value).FirstOrDefault();
                if (match != null)
                {
                    var url = match.Groups[1].Value;

                    return url.Contains("/") ?
                        url.Substring(0, url.IndexOf("/"))
                        : url;
                }
            }

            return "...";
        }
    }
}
