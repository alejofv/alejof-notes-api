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
        public class Content
        {
            public string Title { get; set; }
            public string Slug { get; set; }
            public string ContentUrl { get; set; }
            public string Date { get; set; }

            public IDictionary<string, string> Data { get; set; }
        }
        
        public class Request : BaseRequest, IRequest<Response> { }
        
        public class Response
        {
            public IReadOnlyCollection<Content> Data { get; set; }
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
                            var model = _mapper.Map<Content>(entity);
                            var noteData = data.Where(d => d.NoteId == entity.Uid);

                            model.Data = (noteData ?? Enumerable.Empty<DataEntity>())
                                .ToDictionary(
                                    keySelector: d => d.Name,
                                    elementSelector: d => d.Value);

                            // Specific data name mappings
                            if (model.Data.ContainsKey(SourceDataName))
                                model.Data.Add(SourceNameDataName, GetUrlDomain(model.Data[SourceDataName]));

                            return model;
                        })
                    .ToList();

                return new Response { Data = content.AsReadOnly() };
            }

            private async Task<(IList<NoteEntity>, IList<DataEntity>)> GetNotes(string tenantId)
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
                if (!string.IsNullOrEmpty(value))
                {
                    var match = LinkParser.Matches(value).FirstOrDefault();
                    if (match != null)
                    {
                        var url = match.Groups[1].Value;

                        return url.Contains("/") ?
                            url.Substring(0, url.IndexOf("/"))
                            : url;
                    }
                }

                return "...";
            }
        }

        public class Profile : AutoMapper.Profile
        {
            public Profile()
            {
                CreateMap<NoteEntity, Content>()
                    .ForMember(m => m.ContentUrl, o => o.MapFrom(e => e.BlobUri))
                    .ForMember(m => m.Date, o => o.MapFrom((e, m) => e.Date.ToString("yyyy-MM-dd")));
            }
        }
    }
}