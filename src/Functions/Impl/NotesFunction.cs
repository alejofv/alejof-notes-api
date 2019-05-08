using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alejof.Notes.Functions.Extensions;
using Alejof.Notes.Functions.Impl.TableStorage;
using Alejof.Notes.Functions.Infrastructure;
using Alejof.Notes.Functions.Mapping;
using Alejof.Notes.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;

namespace Alejof.Notes.Functions.Impl
{
    public class NotesFunction : IFunction
    {
        public ILogger Log { get; set; }
        public FunctionSettings Settings { get; set; }

        public async Task<IReadOnlyCollection<object>> GetNotes(bool published)
        {
            var storageAccount = CloudStorageAccount.Parse(Settings.StorageConnectionString);
            
            var tableClient = storageAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference(NoteEntity.TableName);

            var key = NoteEntity.GetDefaultKey(published);
            Log.LogInformation($"Getting notes from storage. TableName: {NoteEntity.TableName}, Key: {key}");

            var notes = await table.ScanAsync<NoteEntity>(key);
            return notes
                .Select(n => n.ToModel())
                .ToList()
                .AsReadOnly();
        }
    }
}
