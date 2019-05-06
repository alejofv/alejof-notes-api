using Alejof.Notes.Functions.Impl.TableStorage;
using Humanizer;

namespace Alejof.Notes.Functions.Mapping
{
    public static class NotesMapper
    {
        public static Models.Note ToModel(this NoteEntity entity) =>
            new Models.Note
            {
                Id = int.Parse(entity.RowKey),
                Type = entity.Type,
                Title = entity.Title,
                Slug = entity.Slug,
                Content = entity.Content,
                Source = entity.Source,
                Date = entity.Date.ToString("yyyy-MM-dd"),
                DateText = entity.Date.Humanize(utcDate: true),
            };
    }
}
