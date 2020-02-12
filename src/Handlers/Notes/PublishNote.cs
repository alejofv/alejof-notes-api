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
    public class PublishNote
    {
        public class Request : BaseRequest, IRequest<ActionResponse>
        {
            public string NoteId { get; set; } = string.Empty;
            public DateTime? Date { get; set; }
            public bool Publish { get; set; }
        }

        public class Handler : IRequestHandler<Request, ActionResponse>
        {
            private readonly CloudTable _noteTable;
            private readonly CloudTable _dataTable;

            public Handler(
                CloudTableClient tableClient)
            {
                this._noteTable = tableClient.GetTableReference(NoteEntity.TableName);
                this._dataTable = tableClient.GetTableReference(DataEntity.TableName);
            }

            public async Task<ActionResponse> Handle(Request request, CancellationToken cancellationToken)
            {
                var (oldNote, oldData) = await GetNote(request.TenantId, request.NoteId, !request.Publish);
                if (oldNote == null)
                    return new ActionResponse { Success = false, Message = "Note not found" };

                var noteDate = request.Date ?? DateTime.UtcNow;
                var newNote = await SaveNote(request.TenantId, request.Publish, noteDate, oldNote);
                if (newNote == null)
                    return new ActionResponse { Success = false, Message = "CreateNote failed" };

                await MoveData(oldData, newNote.PartitionKey);
                await _noteTable.DeleteAsync(oldNote);

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

            private async Task<NoteEntity?> SaveNote(string tenantId, bool published, DateTime noteDate, NoteEntity entity)
            {
                var newEntity = NoteEntity
                    .New(tenantId, published, noteDate);

                newEntity.Uid = entity.Uid;
                newEntity.Slug = entity.Slug;
                newEntity.Title = entity.Title;
                newEntity.BlobUri = entity.BlobUri;

                var result = await _noteTable.InsertAsync(entity);
                if (result)
                    return entity;

                return null;
            }

            private async Task MoveData(List<DataEntity> oldData, string newKey)
            {
                if (oldData.Any())
                {
                    var newData = oldData
                        .Select(
                            d => new DataEntity
                            {
                                PartitionKey = newKey,
                                RowKey = d.RowKey,
                                Value = d.Value,
                            })
                        .ToList();

                    var insertBatch = new TableBatchOperation();
                    newData.ForEach(d => insertBatch.Insert(d));

                    var deleteBatch = new TableBatchOperation();
                    oldData.ForEach(d => deleteBatch.Delete(d));

                    await Task.WhenAll(
                        _dataTable.ExecuteBatchAsync(insertBatch),
                        _dataTable.ExecuteBatchAsync(deleteBatch));
                }
            }
        }
    }
}