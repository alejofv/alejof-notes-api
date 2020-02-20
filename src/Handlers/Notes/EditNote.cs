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
            public bool Published { get; set; }

            public string Title { get; set; } = string.Empty;
            public string Slug { get; set; } = string.Empty;
            public string Format { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public IDictionary<string, string?> Data { get; set; } = new Dictionary<string, string?>();

            public object AuditRecord => new
            {
                this.NoteId,
                this.Published,
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
                var (note, oldData) = await GetNote(request.TenantId, request.NoteId, request.Published);
                if (note == null)
                    return new ActionResponse { Success = false, Message = "Note not found" };

                var oldContentUri = note.BlobUri;
                var filename = GetNoteFilename(request.TenantId, note.Date, request.Slug, request.Format);
                var uri = await _container.UploadAsync(request.Content, filename);
                
                var result = await SaveNote(request, note, uri);
                if (!result)
                    return new ActionResponse { Success = false, Message = "UpdateNote failed" };

                // Update data
                await SaveData(note, request.Data, oldData);
            
                if (!string.IsNullOrWhiteSpace(oldContentUri) && !string.Equals(uri, oldContentUri, StringComparison.OrdinalIgnoreCase))
                    await _container.DeleteAsync(oldContentUri);

                return ActionResponse.Ok;
            }

            private async Task<(NoteEntity?, List<DataEntity>)> GetNote(string tenantId, string id, bool published)
            {
                var note = await _noteTable.RetrieveAsync<NoteEntity>(NoteEntity.GetKey(tenantId, published), id);
                var data = note != null ? 
                    await _dataTable.QueryAsync<DataEntity>(note.PartitionKey, FilterBy.RowKey.Like(note.Uid))
                    : Enumerable.Empty<DataEntity>().ToList();

                return (note, data);
            }

            private async Task<bool> SaveNote(Request request, NoteEntity entity, string contentUri)
            {
                entity.Title = request.Title;
                entity.Slug = request.Slug;
                entity.BlobUri = contentUri;

                // Fix legacy notes
                if (string.IsNullOrEmpty(entity.Uid))
                    entity.Uid = Guid.NewGuid().ToString();

                return await _noteTable.ReplaceAsync(entity);
            }

            private async Task SaveData(NoteEntity note, IDictionary<string, string?> newData, IEnumerable<DataEntity> oldData)
            {
                var entities = newData
                    .Select(
                        entry => new DataEntity
                        {
                            PartitionKey = note.PartitionKey,
                            RowKey = $"{note.Uid}_{entry.Key}",
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

            private string GetNoteFilename(string tenantId, DateTime date, string slug, string format) => $"{tenantId}/{date.ToString("yyyy-MM-dd")}-{slug}.{format}";
        }
    }
}
