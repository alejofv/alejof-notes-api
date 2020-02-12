#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Alejof.Notes.Storage;
using AutoMapper;
using MediatR;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Handlers
{
    public static class GetContent
    {
        public class ContentModel
        {
            public string Title { get; set; } = string.Empty;
            public string Slug { get; set; } = string.Empty;
            public string ContentUrl { get; set; } = string.Empty;
            public string Date { get; set; } = string.Empty;

            public IDictionary<string, string?> Data { get; set; } = new Dictionary<string, string?>();
        }
        
        public class Request : BaseRequest, IRequest<Response> { }

        public class Response
        {
            public IReadOnlyCollection<ContentModel> Data { get; private set; }
            public Response(List<ContentModel> data)
            {
                this.Data = data.AsReadOnly();
            }
        }

        public class Handler : IRequestHandler<Request, Response>
        {
            private readonly CloudTable _noteTable;
            private readonly CloudTable _dataTable;
            private readonly IMapper _mapper;


            private const string SourceDataName = "Source";
            private const string SourceNameDataName = "SourceName";

            public Handler(
                CloudTableClient tableClient,
                IMapper mapper)
            {
                this._noteTable = tableClient.GetTableReference(NoteEntity.TableName);
                this._dataTable = tableClient.GetTableReference(DataEntity.TableName);
                this._mapper = mapper;
            }

            public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
            {
                var (notes, data) = await GetNotes(request.TenantId);
                var content = notes
                    .Select(
                        entity => 
                        {
                            var model = _mapper.Map<ContentModel>(entity);
                            var noteData = data.Where(d => d.NoteId == entity.Uid);

                            model.Data = noteData
                                .ToDictionary(
                                    keySelector: d => d.Name!,
                                    elementSelector: d => d.Value);

                            // Specific data name mappings
                            if (model.Data.TryGetValue(SourceDataName, out var sourceValue) && !string.IsNullOrWhiteSpace(sourceValue))
                                model.Data.Add(SourceNameDataName, GetUrlDomain(sourceValue));

                            return model;
                        })
                    .ToList();

                return new Response(content);
            }

            private async Task<(List<NoteEntity>, List<DataEntity>)> GetNotes(string tenantId)
            {
                var key = NoteEntity.GetKey(tenantId, true);
                
                var notesTask = _noteTable.ScanAsync<NoteEntity>(key);
                var dataTask = _dataTable.ScanAsync<DataEntity>(key);

                await Task.WhenAll(notesTask, dataTask);
                return (notesTask.Result, dataTask.Result);
            }

            private static readonly Regex LinkParser = new Regex(@"\b(?:https?:\/\/|www\.)([^ \f\n\r\t\v\]]+)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            public static string GetUrlDomain(string value)
            {
                var match = LinkParser.Matches(value).FirstOrDefault();
                if (match != null)
                {
                    var url = match.Groups[1].Value;

                    return url.Contains("/") ?
                        url.Substring(0, url.IndexOf("/"))
                        : url;
                }

                return "...";
            }
        }

        public class Profile : AutoMapper.Profile
        {
            public Profile()
            {
                CreateMap<NoteEntity, ContentModel>()
                    .ForMember(m => m.ContentUrl, o => o.MapFrom(e => e.BlobUri))
                    .ForMember(m => m.Date, o => o.MapFrom((e, m) => e.Date.ToString("yyyy-MM-dd")));
            }
        }
    }
}