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

namespace Alejof.Notes.Handlers
{
    public class PublishNote
    {
        public class Request : BaseRequest, IRequest<ActionResponse>, IAuditableRequest
        {
            public string NoteId { get; set; } = string.Empty;
            public DateTime? Date { get; set; }
            public bool Publish { get; set; }

            public object AuditRecord => new
            {
                this.NoteId,
                this.Date,
                this.Publish,
            };
        }

        public class Handler : IRequestHandler<Request, ActionResponse>
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

            public async Task<ActionResponse> Handle(Request request, CancellationToken cancellationToken)
            {
                var (note, data, content) = await GetNote(request.TenantId, request.NoteId);
                if (note == null)
                    return new ActionResponse { Success = false, Message = "Note not found" };

                if (string.IsNullOrWhiteSpace(note.Slug))
                    return new ActionResponse { Success = false, Message = "Note slug not valid" };

                var noteDate = request.Date ?? DateTime.UtcNow;
                var format = System.IO.Path.GetExtension(note.BlobUri) ?? ".md";

                // Create published blob
                var filename = GetNoteFilename(request.TenantId, noteDate, note.Slug, format.Replace(".", ""));
                var newContent = ProcessContent(data, content);
                var uri = await _container.UploadAsync(content, filename);

                var result = await SaveNote(note, uri);
                if (!result)
                    return new ActionResponse { Success = false, Message = "UpdateNote failed" };

                return ActionResponse.Ok;
            }

            private async Task<(NoteEntity?, IList<DataEntity>, string)> GetNote(string tenantId, string id)
            {
                var noteTask = _noteTable.RetrieveAsync<NoteEntity>(tenantId, id);
                var dataTask = _dataTable.QueryAsync<DataEntity>(tenantId, FilterBy.RowKey.Like(id));

                await Task.WhenAll(noteTask, dataTask);
                var note = noteTask.Result;

                // Get content from blob
                var content = string.Empty;
                if (!string.IsNullOrEmpty(note?.BlobUri))
                    content = await _container.DownloadAsync(note.BlobUri);

                return (note, dataTask.Result, content);
            }

            private async Task<bool> SaveNote(NoteEntity entity, string publishedBlobUri)
            {
                entity.PublishedBlobUri = publishedBlobUri;
                entity.IsPublished = !string.IsNullOrWhiteSpace(publishedBlobUri);

                return await _noteTable.ReplaceAsync(entity);
            }

            // Add data as Front matter
            // TODO: use configurable data by tenant
            private string ProcessContent(IList<DataEntity> data, string content)
                => new StringBuilder()
                    .AppendLine("---")
                    .AppendItems(data, d => $"{d.Name}: \"{d.Value}\"")
                    .AppendLine("---")
                    .AppendLine(content)
                    .ToString();

            private string GetNoteFilename(string tenantId, DateTime date, string slug, string format)
                => $"{tenantId}/{date.ToString("yyyy-MM-dd")}-{slug}.{format}";
        }
    }
}
