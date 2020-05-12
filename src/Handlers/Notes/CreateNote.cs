#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alejof.Notes.Storage;
using AutoMapper;
using MediatR;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Handlers
{
    public class CreateNote
    {
        public class Request : BaseRequest, IRequest<Response>, IAuditableRequest
        {
            public string Title { get; set; } = string.Empty;
            public string Slug { get; set; } = string.Empty;
            public string Format { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public IDictionary<string, string?> Data { get; set; } = new Dictionary<string, string?>();

            public object AuditRecord => new
            {
                this.Title,
                this.Slug,
                this.Format,
                this.Data,
            };
        }

        public class Response : ActionResponse
        {
            public string NoteId { get; set; } = string.Empty;
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
                var guid = Guid.NewGuid().ToString();

                var filename = GetNoteFilename(request.TenantId, guid, request.Format);
                var uri = await _container.UploadAsync(request.Content, filename);

                var note = await SaveNote(request, guid, uri);
                if (note == null)
                    return new Response { Message = "CreateNote failed" };

                // Create and insert data
                await SaveData(note, request.Data);
                return new Response { Success = true, NoteId = note.RowKey  };
            }

            private async Task<NoteEntity?> SaveNote(Request request, string guid, string contentUri)
            {
                var entity = new NoteEntity
                {
                    PartitionKey = request.TenantId,
                    RowKey = guid,
                    Title = request.Title,
                    Slug = request.Slug,
                    BlobUri = contentUri,
                };

                var result = await _noteTable.InsertAsync(entity);
                if (result)
                    return entity;

                return null;
            }

            private async Task SaveData(NoteEntity note, IDictionary<string, string?> data)
            {
                var entities = data
                    .Where(d => !string.IsNullOrWhiteSpace(d.Value))
                    .Select(
                        entry => new DataEntity
                        {
                            PartitionKey = note.PartitionKey,
                            RowKey = $"{note.RowKey}-{entry.Key}",
                            Value = entry.Value,
                        })
                    .ToList();
                
                if (data.Any())
                {
                    var batch = new TableBatchOperation();
                    entities.ForEach(d => batch.Insert(d));

                    await _dataTable.ExecuteBatchAsync(batch);
                }
            }

            private string GetNoteFilename(string tenantId, string guid, string format) => $"{tenantId}/{guid}.{format}";
        }
    }
}
