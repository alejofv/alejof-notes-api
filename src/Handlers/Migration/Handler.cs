#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alejof.Notes.Extensions;
using Alejof.Notes.Storage;
using AutoMapper;
using MediatR;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Handlers.Migration
{
    public class Request : BaseRequest, IRequest<Response>
    {
        public bool Published { get; set; }
        public bool PreserveOldEntries { get; set; }
        public string? BlobContainerReplacement { get; set; }
    }

    public class Response : ActionResponse
    {
        public IReadOnlyCollection<(string NoteId, DateTime PublishDate)> PublishSignals { get; private set; }

        public Response(List<(string NoteId, DateTime PublishDate)>? publishSignals = null)
        {
            this.PublishSignals = (publishSignals ?? new List<(string NoteId, DateTime PublishDate)>()).AsReadOnly();
            this.Success = true;
        }
    }

    public class Handler : IRequestHandler<Request, Response>
    {
        private readonly CloudTable _noteTable;
        private readonly CloudTable _dataTable;
        private readonly CloudBlobContainer _container;

        public Handler(
            CloudTableClient tableClient,
            CloudBlobClient blobClient)
        {
            this._noteTable = tableClient.GetTableReference(NoteEntity.TableName);
            this._dataTable = tableClient.GetTableReference(DataEntity.TableName);

            this._container = blobClient.GetContainerReference(Blobs.ContentContainerName);
        }

        public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
        {
            // Get all notes using old format
            var legacyNotes = await GetNotes(request.TenantId, request.Published);

            // Copy data to new format AS NOT PUBLISHED
            // Copy data properties
            var newNotes = BuildNewNotes(request.TenantId, legacyNotes.Notes, legacyNotes.Data, request.BlobContainerReplacement);
            await SaveNewNotes(newNotes.Notes.Select(t => t.Note).ToList(), newNotes.Data);

            // If Published, send signal to publish USING ORIGINAL NOTE DATE
            if (request.Published)
                return new Response(newNotes.Notes.Select(t => (t.Note.RowKey, t.Date)).ToList());

            return new Response();
        }

        private async Task<(IList<LegacyNoteEntity> Notes, IList<LegacyDataEntity> Data)> GetNotes(string tenantId, bool published)
        {
            var legacyKey = $"{tenantId}_{(published ? "published" : "draft")}";

            var notesTask = _noteTable.ScanAsync<LegacyNoteEntity>(legacyKey);
            var dataTask = _dataTable.ScanAsync<LegacyDataEntity>(legacyKey);

            await Task.WhenAll(notesTask, dataTask);
            return (notesTask.Result, dataTask.Result);
        }

        private (IList<(NoteEntity Note, DateTime Date)> Notes, IList<DataEntity> Data) BuildNewNotes(string tenantId, IList<LegacyNoteEntity> notes, IList<LegacyDataEntity> data, string? blobContainerReplacement)
        {
            var newNotes = new List<(NoteEntity NoteEntity, DateTime Date)>();
            var newDataItems = new List<DataEntity>();

            var replacement = (Old: string.Empty, New: string.Empty);
            if (blobContainerReplacement?.Contains("=") == true)
                replacement = (blobContainerReplacement.Split('=')[0], blobContainerReplacement.Split('=')[1]);

            foreach (var note in notes)
            {
                var newNote = new NoteEntity
                {
                    PartitionKey = tenantId,
                    RowKey = Guid.NewGuid().ToString("N"),
                    Title = note.Title,
                    Slug = note.Slug,
                    BlobUri = note.BlobUri?.Replace(replacement.Old, replacement.New),
                };

                var newData = data
                    .Where(entry => entry.NoteId == note.Uid)
                    .Select(
                        entry => new DataEntity
                        {
                            PartitionKey = tenantId,
                            RowKey = $"{newNote.RowKey}-{entry.Name}",
                            Value = entry.Value,
                        })
                    .ToList();

                newNotes.Add((newNote, note.Date));
                newDataItems.AddRange(newData);
            }

            return (newNotes, newDataItems);
        }

        private async Task SaveNewNotes(IList<NoteEntity> notes, IList<DataEntity> data)
        {
            var notesBatch = new TableBatchOperation();
            notes.ToList().ForEach(d => notesBatch.Insert(d));
            var notesTask = _noteTable.ExecuteBatchAsync(notesBatch);

            var dataBatch = new TableBatchOperation();
            data.ToList().ForEach(d => dataBatch.Insert(d));
            var dataTask = _dataTable.ExecuteBatchAsync(dataBatch);

            await Task.WhenAll(notesTask, dataTask);
        }
    }
}
