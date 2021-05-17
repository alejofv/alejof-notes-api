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
            public string Title { get; set; } = string.Empty;
            public string Slug { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public IDictionary<string, string?> Data { get; set; } = new Dictionary<string, string?>();
        }

        public class AllNotesRequest : BaseRequest, IRequest<Response>
        {
            public bool Published { get; set; }
        }
        
        public class SingleNoteRequest : BaseRequest, IRequest<Response>
        {
            public string NoteId { get; set; } = string.Empty;
        }

        public class Response
        {
            public IReadOnlyCollection<NoteModel> Data { get; private set; }

            public Response(List<NoteModel> data)
            {
                this.Data = data.AsReadOnly();
            }
        }

        public class Handler : IRequestHandler<AllNotesRequest, Response>, IRequestHandler<SingleNoteRequest, Response>
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
                _noteTable = tableClient.GetTableReference(NoteEntity.TableName);
                _dataTable = tableClient.GetTableReference(DataEntity.TableName);

                _container = blobClient.GetContainerReference(Blobs.ContentContainerName);
                _mapper = mapper;
            }

            public async Task<Response> Handle(AllNotesRequest request, CancellationToken cancellationToken)
            {
                var result = new List<NoteModel>();

                var (notes, data) = await GetNotes(request.TenantId);
                result.AddRange(
                    notes
                        .Where(note => note.IsPublished == request.Published)
                        .Select(entity => MapToModel(entity, data.Where(d => d.NoteId == entity.RowKey))));
                
                return new Response(result);
            }

            public async Task<Response> Handle(SingleNoteRequest request, CancellationToken cancellationToken)
            {
                var result = new List<NoteModel>();

                var (note, data, content) = await GetNote(request.TenantId, request.NoteId);
                if (note != null)
                {
                    var model = MapToModel(note, data);
                    model.Content = content;

                    result.Add(model);
                }

                return new Response(result);
            }

            private async Task<(IList<NoteEntity>, IList<DataEntity>)> GetNotes(string tenantId)
            {
                var tasks = (
                    _noteTable.ScanAsync<NoteEntity>(tenantId),
                    _dataTable.ScanAsync<DataEntity>(tenantId));
                    
                await Task.WhenAll(tasks.Item1, tasks.Item2);
                return (tasks.Item1.Result, tasks.Item2.Result);
            }

            private async Task<(NoteEntity?, IList<DataEntity>, string)> GetNote(string tenantId, string id)
            {
                var tasks = (
                    _noteTable.RetrieveAsync<NoteEntity>(tenantId, id),
                    _dataTable.QueryAsync<DataEntity>(tenantId, FilterBy.RowKey.Like(id)));

                await Task.WhenAll(tasks.Item1, tasks.Item2);
                var (note, data) = (tasks.Item1.Result, tasks.Item2.Result);

                // Get content from blob
                var content = string.Empty;
                if (!string.IsNullOrEmpty(note?.BlobUri))
                    content = await _container.DownloadAsync(note.BlobUri);

                return (note, data, content);
            }

            private NoteModel MapToModel(NoteEntity entity, IEnumerable<DataEntity> data)
            {
                var model = _mapper.Map<NoteModel>(entity);

                model.Data = data
                    .ToDictionary(
                        keySelector: d => d.Name.Camelize(),
                        elementSelector: d => d.Value);

                return model;
            }
        }

        public class Profile : AutoMapper.Profile
        {
            public Profile()
            {
                CreateMap<NoteEntity, NoteModel>()
                    .ForMember(m => m.Id, o => o.MapFrom(e => e.RowKey));
            }
        }
    }
}
