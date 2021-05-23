#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alejof.Notes.Storage;
using Humanizer;
using MediatR;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Handlers.Content
{
    public class Request : BaseRequest, IRequest<Response>
    {
        
    }

    public class Response
    {
        public IReadOnlyCollection<ContentModel> ContentList { get; private set; }

        public Response(IEnumerable<ContentModel?> contentEnumerable)
        {
            this.ContentList = contentEnumerable
                .Where(x => x != null)
                .Select(x => x!)
                .ToList()
                .AsReadOnly();
        }
    }

    public class Handler : IRequestHandler<Request, Response>
    {
        private readonly CloudTable _noteTable;
        private readonly CloudTable _dataTable;
        private readonly CloudBlobContainer _container;

        public Handler(
            CloudTableClient tableClient,
            CloudBlobClient blobClient)
        {
            _noteTable = tableClient.GetTableReference(NoteEntity.TableName);
            _dataTable = tableClient.GetTableReference(DataEntity.TableName);
            
            _container = blobClient.GetContainerReference(Blobs.PublishContainerName);
        }

        public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
        {
            var tasks = (
                _container.ListBlobsSegmentedAsync($"{request.TenantId}/", null),
                _noteTable.ScanAsync<NoteEntity>(request.TenantId),
                _dataTable.ScanAsync<DataEntity>(request.TenantId));

            await Task.WhenAll(tasks.Item1, tasks.Item2, tasks.Item3);
            var (items, notes, data) = (tasks.Item1.Result, tasks.Item2.Result, tasks.Item3.Result);

            // Build SAS with 15-minute expiration
            var adHocSAPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(5),
                Permissions = SharedAccessBlobPermissions.Read
            };
            
            return new Response(
                    (from item in (items.Results.OfType<CloudBlockBlob>())
                        join note in notes
                            on item.Uri.ToString() equals note.PublishedBlobUri
                        join d in data
                            on note.RowKey equals d.NoteId into dataItems
                        where note.IsPublished
                    select (item, note, dataItems))
                .Select(
                    x => new ContentModel
                    {
                        Url = x.item.Uri + x.item.GetSharedAccessSignature(adHocSAPolicy),
                        Date = x.note.PublishedDate,
                        Slug = x.note.Slug,
                        Title = x.note.Title,
                        Data = x.dataItems
                            .ToDictionary(
                                keySelector: d => d.Name.Camelize(),
                                elementSelector: d => d.Value),
                    })
                .OrderByDescending(x => x?.Date));
        }
    }

    public class ContentModel
    {
        public string Url { get; set; } = string.Empty;
        public string? Date { get; set; }
        public string? Slug { get; set; }
        public string? Title { get; set; }
        public Dictionary<string, string?> Data { get; set; } = new Dictionary<string, string?>();
    }
}
