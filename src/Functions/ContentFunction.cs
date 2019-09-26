using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alejof.Notes.Extensions;
using Alejof.Notes.Infrastructure;
using Alejof.Notes.Functions.Mapping;
using Alejof.Notes.Functions.TableStorage;
using Alejof.Notes.Models;
using Alejof.Notes.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Functions
{
    public class ContentFunction : IFunction
    {
        public ILogger Log { get; set; }
        public FunctionSettings Settings { get; set; }

        private CloudTable _noteTable = null;
        private CloudTable _dataTable = null;
        private CloudTable NoteTable => _noteTable = _noteTable ?? Settings.StorageConnectionString.GetTable(NoteEntity.TableName);
        private CloudTable DataTable => _dataTable = _dataTable ?? Settings.StorageConnectionString.GetTable(DataEntity.TableName);

        public async Task<IReadOnlyCollection<Content>> GetContent(string tenantId)
        {
            var key = NoteEntity.GetKey(tenantId, true);
            Log.LogInformation($"Getting notes from storage. TableName: {NoteEntity.TableName}, Key: {key}");

            var notesTask = NoteTable.ScanAsync<NoteEntity>(key);
            var dataTask = DataTable.ScanAsync<DataEntity>(key);

            await Task.WhenAll(notesTask, dataTask);

            return notesTask.Result
                .Select(
                    n =>
                    {
                        var data = dataTask.Result
                            .Where(d => d.NoteId == n.Uid);

                        return n.MapToContentModel(data);
                    })
                .ToList()
                .AsReadOnly();
        }
    }
}
