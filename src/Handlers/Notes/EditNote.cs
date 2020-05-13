#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alejof.Notes.Storage;
using MediatR;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Handlers
{
    public class EditNote
    {
        public class Request : BaseRequest, IRequest<ActionResponse>, IAuditableRequest
        {
            public string NoteId { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Slug { get; set; } = string.Empty;
            public string Format { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public IDictionary<string, string?> Data { get; set; } = new Dictionary<string, string?>();

            public object AuditRecord => new
            {
                this.NoteId,
                this.Title,
                this.Slug,
                this.Format,
                this.Data,
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
                var (note, oldData) = await GetNote(request.TenantId, request.NoteId);
                if (note == null)
                    return new ActionResponse { Success = false, Message = "Note not found" };

                var filename = GetNoteFilename(request.TenantId, request.NoteId, request.Format);
                var uri = await _container.UploadAsync(request.Content, filename);
                
                var result = await SaveNote(request, note, uri);
                if (!result)
                    return new ActionResponse { Success = false, Message = "UpdateNote failed" };

                // Update data
                await SaveData(note, request.Data, oldData);
                return ActionResponse.Ok;
            }

            private async Task<(NoteEntity?, List<DataEntity>)> GetNote(string tenantId, string id)
            {
                var noteTask = _noteTable.RetrieveAsync<NoteEntity>(tenantId, id);
                var dataTask = _dataTable.QueryAsync<DataEntity>(tenantId, FilterBy.RowKey.Like(id));

                await Task.WhenAll(noteTask, dataTask);
                return (noteTask.Result, dataTask.Result);
            }

            private async Task<bool> SaveNote(Request request, NoteEntity entity, string contentUri)
            {
                entity.Title = request.Title;
                entity.Slug = request.Slug;
                entity.BlobUri = contentUri;

                return await _noteTable.ReplaceAsync(entity);
            }

            private async Task SaveData(NoteEntity note, IDictionary<string, string?> newData, IEnumerable<DataEntity> oldData)
            {
                var entities = newData
                    .Where(d => !string.IsNullOrWhiteSpace(d.Value))
                    .Select(
                        entry => new DataEntity
                        {
                            PartitionKey = note.PartitionKey,
                            RowKey = $"{note.RowKey}_{entry.Key}",
                            Value = entry.Value,
                        })
                    .ToList();
                
                if (entities.Any() || oldData.Any())
                {
                    var batch = new TableBatchOperation();
                    entities.ForEach(d => batch.InsertOrReplace(d));

                    var dataNames = newData.Keys;
                    oldData
                        .Where(d => !dataNames.Contains(d.Name))
                        .ToList()
                        .ForEach(d => batch.Delete(d));

                    await _dataTable.ExecuteBatchAsync(batch);
                }
            }

            private string GetNoteFilename(string tenantId, string guid, string format) => $"{tenantId}/{guid}.{format}";
        }
    }
}
