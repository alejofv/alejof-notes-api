using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Alejof.Notes.Storage;
using AutoMapper;
using MediatR;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Handlers
{
    public class DeleteMedia
    {
        public class Request : BaseRequest, IRequest<ActionResponse>
        {
            public string MediaId { get; set; }
        }

        public class Handler : IRequestHandler<Request, ActionResponse>
        {
            private readonly CloudTable _mediaTable;
            private readonly CloudBlobContainer _container;

            public Handler(
                CloudTableClient tableClient,
                CloudBlobClient blobClient)
            {
                this._mediaTable = tableClient.GetTableReference(MediaEntity.TableName);
                this._container = blobClient.GetContainerReference(Blobs.ContentContainerName);
            }

            public async Task<ActionResponse> Handle(Request request, CancellationToken cancellationToken)
            {
                var entity = await _mediaTable.RetrieveAsync<NoteEntity>(request.TenantId, request.MediaId);
                if (entity == null)
                    return new ActionResponse { Success = false, Message = "Media not found" };

                var resultTask = _mediaTable.DeleteAsync(entity);
                var deleteTask = _container.DeleteAsync(entity.BlobUri);

                await Task.WhenAll(resultTask, deleteTask);
                if (!resultTask.Result)
                    return new ActionResponse { Success = false, Message = "DeleteMedia failed" };

                return ActionResponse.Ok;
            }
        }
    }
}