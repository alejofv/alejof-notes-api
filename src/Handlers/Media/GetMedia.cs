#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alejof.Notes.Storage;
using AutoMapper;
using MediatR;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Handlers
{
    public class GetMedia
    {
        public class MediaModel
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string BlobUri { get; set; } = string.Empty;
        }

        public class Request : BaseRequest, IRequest<Response> { }

        public class Response
        {
            public IReadOnlyCollection<MediaModel> Data { get; private set; }
            public Response(List<MediaModel> data)
            {
                this.Data = data.AsReadOnly();
            }
        }

        public class Handler : IRequestHandler<Request, Response>
        {
            private readonly CloudTable _mediaTable;
            private readonly IMapper _mapper;

            public Handler(
                CloudTableClient tableClient,
                IMapper mapper)
            {
                this._mediaTable = tableClient.GetTableReference(MediaEntity.TableName);
                this._mapper = mapper;
            }

            public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
            {
                var entities = await _mediaTable.ScanAsync<MediaEntity>(request.TenantId);

                var result = _mapper.Map<IEnumerable<MediaEntity>, List<MediaModel>>(entities);
                return new Response(result);
            }
        }

        public class Profile : AutoMapper.Profile
        {
            public Profile()
            {
                CreateMap<MediaEntity, MediaModel>()
                    .ForMember(m => m.Id, o => o.MapFrom(e => e.RowKey));   
            }
        }
    }
}
