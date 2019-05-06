using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alejof.Notes.Functions.Extensions;
using Alejof.Notes.Functions.Impl.TableStorage;
using Alejof.Notes.Functions.Mapping;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;

namespace Alejof.Notes.Functions.Impl
{
    public class NotesFunction
    {
        private readonly ILogger _log;
        private readonly Settings.FunctionSettings _settings;
        private readonly CloudStorageAccount _storageAccount;

        public NotesFunction(
            ILogger log,
            Settings.FunctionSettings settings)
        {
            this._log = log;
            this._settings = settings;
            
            this._storageAccount = CloudStorageAccount.Parse(_settings.StorageConnectionString);
        }

        public async Task<IReadOnlyCollection<object>> GetNotes(bool published)
        {
            var tableClient = _storageAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference(NoteEntity.TableName);

            var key = NoteEntity.GetDefaultKey(published);
            _log.LogInformation($"Getting notes from storage. TableName: {NoteEntity.TableName}, Key: {key}");

            var notes = await table.ScanAsync<NoteEntity>(key);
            return notes
                .Select(n => n.ToModel())
                .ToList()
                .AsReadOnly();
        }
    }
}
