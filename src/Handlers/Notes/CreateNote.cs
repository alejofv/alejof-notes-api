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
        public class Request : BaseRequest, IRequest<ActionResponse>
        {
            public string Title { get; set; } = string.Empty;
            public string Slug { get; set; } = string.Empty;
            public string Format { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public IDictionary<string, string?> Data { get; set; } = new Dictionary<string, string?>();
        }

        public class Handler : IRequestHandler<Request, ActionResponse>
        {
            private readonly CloudTable _noteTable;
            private readonly CloudTable _dataTable;
            private readonly CloudBlobContainer _container;
            private readonly IMapper _mapper;

            public Handler(
                CloudTableClient tableClient,
                CloudBlobClient blobClient,
                IMapper mapper)
            {
                this._noteTable = tableClient.GetTableReference(NoteEntity.TableName);
                this._dataTable = tableClient.GetTableReference(DataEntity.TableName);

                this._container = blobClient.GetContainerReference(Blobs.ContentContainerName);
                this._mapper = mapper;
            }

            public async Task<ActionResponse> Handle(Request request, CancellationToken cancellationToken)
            {
                var noteDate = DateTime.UtcNow;

                var filename = GetNoteFilename(request.TenantId, noteDate, request.Slug, request.Format);
                var uri = await _container.UploadAsync(request.Content, filename);

                var note = await SaveNote(request, noteDate, uri);
                if (note == null)
                    return new ActionResponse { Success = false, Message = "CreateNote failed" };

                // Create and insert data
                await SaveData(note, request.Data);
                return ActionResponse.Ok;
            }

            private async Task<NoteEntity?> SaveNote(Request request, DateTime noteDate, string contentUri)
            {
                var entity = NoteEntity
                    .New(request.TenantId, false, noteDate);

                entity.Title = request.Title;
                entity.Slug = request.Slug;
                entity.BlobUri = contentUri;

                var result = await _noteTable.InsertAsync(entity);
                if (result)
                    return entity;

                return null;
            }

            private async Task SaveData(NoteEntity note, IDictionary<string, string?> data)
            {
                var entities = data
                    .Select(
                        entry => new DataEntity
                        {
                            PartitionKey = note.PartitionKey,
                            RowKey = $"{note.Uid}_{entry.Key}",
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

            private string GetNoteFilename(string tenantId, DateTime date, string slug, string format) => $"{tenantId}/{date.ToString("yyyy-MM-dd")}-{slug}.{format}";
        }
    }
}