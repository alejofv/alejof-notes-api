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
    public class DeleteNote
    {
        public class Request : BaseRequest, IRequest<ActionResponse>, IAuditableRequest
        {
            public string NoteId { get; set; } = string.Empty;

            public object AuditRecord => new
            {
                this.NoteId,
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
                var (entity, data) = await GetNote(request.TenantId, request.NoteId);
                if (entity == null)
                    return new ActionResponse { Success = false, Message = "Note not found" };

                var result = await _noteTable.DeleteAsync(entity);
                if (!result)
                    return new ActionResponse { Success = false, Message = "DeleteNote failed" };

                if (data.Any())
                {
                    var batch = new TableBatchOperation();
                    data.ForEach(d => batch.Delete(d));

                    await _dataTable.ExecuteBatchAsync(batch);
                }

                if (!string.IsNullOrWhiteSpace(entity.BlobUri))
                    await _container.DeleteAsync(entity.BlobUri);
                    
                return ActionResponse.Ok;
            }

            private async Task<(NoteEntity?, List<DataEntity>)> GetNote(string tenantId, string id)
            {
                var noteTask = _noteTable.RetrieveAsync<NoteEntity>(tenantId, id);
                var dataTask = _dataTable.QueryAsync<DataEntity>(tenantId, FilterBy.RowKey.Like(id));

                await Task.WhenAll(noteTask, dataTask);
                return (noteTask.Result, dataTask.Result);
            }
        }
    }
}
