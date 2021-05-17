#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alejof.Notes.Extensions;
using Alejof.Notes.Storage;
using AutoMapper;
using Humanizer;
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
            public ContentFormat Format { get; set; }

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

                this._container = blobClient.GetContainerReference(Blobs.PublishContainerName);
            }

            public async Task<ActionResponse> Handle(Request request, CancellationToken cancellationToken)
            {
                var (note, data) = await GetNote(request.TenantId, request.NoteId);
                if (note == null)
                    return new ActionResponse { Success = false, Message = "Note not found" };

                if (request.Publish)
                {
                    if (string.IsNullOrWhiteSpace(note.Slug))
                        return new ActionResponse { Success = false, Message = "Note slug not valid" };

                    // Create published blob
                    var content = await GetContent(note, data, request.Format);
                    var filename = GetNoteFilename(request, note);

                    note.PublishedBlobUri = await _container.UploadAsync(content, filename);
                }
                else
                {
                    // Delete published blob
                    if (!string.IsNullOrWhiteSpace(note.PublishedBlobUri))
                        await _container.DeleteAsync(note.PublishedBlobUri);

                    note.PublishedBlobUri = null;
                }

                var result = await SaveNote(note, request.Publish);
                if (!result)
                    return new ActionResponse { Success = false, Message = "UpdateNote failed" };

                return ActionResponse.Ok;
            }

            private async Task<(NoteEntity?, IList<DataEntity>)> GetNote(string tenantId, string id)
            {
                var noteTask = _noteTable.RetrieveAsync<NoteEntity>(tenantId, id);
                var dataTask = _dataTable.QueryAsync<DataEntity>(tenantId, FilterBy.RowKey.Like(id));

                await Task.WhenAll(noteTask, dataTask);
                var note = noteTask.Result;

                return (note, dataTask.Result);
            }

            private async Task<bool> SaveNote(NoteEntity entity, bool published)
            {
                entity.IsPublished = published;
                return await _noteTable.ReplaceAsync(entity);
            }

            // Add data as Front matter
            // TODO: use configurable data by tenant
            private async Task<string> GetContent(NoteEntity note, IList<DataEntity> data, ContentFormat format)
            {
                // Get content from blob
                var content = string.Empty;
                if (!string.IsNullOrEmpty(note.BlobUri))
                    content = await _container.DownloadAsync(note.BlobUri);

                return format switch 
                {
                    ContentFormat.File => new StringBuilder()
                        .AppendLine("---")
                        .AppendLine($"layout: note_entry")
                        .AppendLine($"title: \"{note.Title}\"")
                        .AppendItems(data, d => $"{d.Name.Camelize()}: \"{d.Value}\"")
                        .AppendLine("---")
                        .AppendLine(content)
                        .ToString(),
                        
                    _ => content,
                };
            }

            private string GetNoteFilename(Request request, NoteEntity note)
                => request.Format switch
                {
                    ContentFormat.File => $"{request.TenantId}/{(request.Date ?? DateTime.UtcNow).ToString("yyyy-MM-dd")}-{note.Slug}{Path.GetExtension(note.BlobUri)}",
                    ContentFormat.Json => $"{request.TenantId}/{note.Slug}.json",

                    _ => $"{request.TenantId}/{Path.GetFileName(note.BlobUri)}",
                };
        }
    }
}
