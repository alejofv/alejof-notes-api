#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alejof.Notes.Storage;
using AutoMapper;
using Humanizer;
using MediatR;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Handlers
{
    public class GetNote
    {
        public class NoteModel
        {
            public string Id { get; set; } = string.Empty;
            public string DateText { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Slug { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public IDictionary<string, string?> Data { get; set; } = new Dictionary<string, string?>();
        }
        
        public class Request : BaseRequest, IRequest<Response>
        {
            public bool Published { get; set; }
            public string? NoteId { get; set; }
        }

        public class Response
        {
            public IReadOnlyCollection<NoteModel> Data { get; private set; }
            public Response(List<NoteModel> data)
            {
                this.Data = data.AsReadOnly();
            }
        }

        public class Handler : IRequestHandler<Request, Response>
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

            public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
            {
                await _noteTable.CreateIfNotExistsAsync();
                await _dataTable.CreateIfNotExistsAsync();
                
                var result = new List<NoteModel>();

                // local mapping function
                NoteModel mapToModel(NoteEntity entity, IEnumerable<DataEntity> data)
                {
                    var model = _mapper.Map<NoteModel>(entity);

                    model.Data = data
                        .ToDictionary(
                            keySelector: d => d.Name,
                            elementSelector: d => d.Value);

                    return model;
                }

                if (string.IsNullOrWhiteSpace(request.NoteId))
                {
                    var (notes, data) = await GetNotes(request.TenantId, request.Published);
                    result.AddRange(
                        notes.Select(
                            entity => mapToModel(entity, data.Where(d => d.NoteId == entity.Uid))));
                }
                else
                {
                    var (note, data, content) = await GetNote(request.TenantId, request.NoteId, request.Published);
                    if (note != null)
                    {
                        var model = mapToModel(note, data);
                        model.Content = content;

                        result.Add(model);
                    }
                }

                return new Response(result);
            }

            private async Task<(IList<NoteEntity>, IList<DataEntity>)> GetNotes(string tenantId, bool published)
            {
                var key = NoteEntity.GetKey(tenantId, published);
                
                var notesTask = _noteTable.ScanAsync<NoteEntity>(key);
                var dataTask = _dataTable.ScanAsync<DataEntity>(key);

                await Task.WhenAll(notesTask, dataTask);
                return (notesTask.Result, dataTask.Result);
            }

            private async Task<(NoteEntity?, IList<DataEntity>, string)> GetNote(string tenantId, string id, bool published)
            {
                var note = await _noteTable.RetrieveAsync<NoteEntity>(NoteEntity.GetKey(tenantId, published), id);
                var data = note != null ?
                    await _dataTable.QueryAsync<DataEntity>(note.PartitionKey, FilterBy.RowKey.Like(note.Uid))
                    : Enumerable.Empty<DataEntity>().ToList();

                // Get content from blob
                var content = string.Empty;
                if (!string.IsNullOrEmpty(note?.BlobUri))
                    content = await _container.DownloadAsync(note.BlobUri);

                return (note, data, content);
            }
        }

        public class Profile : AutoMapper.Profile
        {
            public Profile()
            {
                CreateMap<NoteEntity, NoteModel>()
                    .ForMember(m => m.Id, o => o.MapFrom(e => e.RowKey))
                    .ForMember(m => m.DateText, o => o.MapFrom((e, m) => e.Date.Humanize(utcDate: true)));
            }
        }
    }
}
