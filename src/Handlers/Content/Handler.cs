#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alejof.Notes.Storage;
using MediatR;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Handlers.Content
{
    public enum ContentFormat
    {
        File,
        Json,
    }

    public class Request : BaseRequest, IRequest<Response>
    {
        public ContentFormat Format { get; set; } = ContentFormat.File;
    }

    public class Response
    {
        public IReadOnlyCollection<BaseContentModel> ContentList { get; private set; }

        public Response(IEnumerable<BaseContentModel> contentEnumerable)
        {
            this.ContentList = contentEnumerable.ToList().AsReadOnly();
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
            var items = await GetContentItems(request.TenantId);

            return new Response(
                request.Format switch
                {
                    ContentFormat.File => ItemsAsJekyllFile(items),
                    ContentFormat.Json => await ItemsAsJsonData(items, request.TenantId),
                    // Unrecognized format
                    _ => Enumerable.Empty<BaseContentModel>(),
                });
        }

        public async Task<IEnumerable<(Uri Url, string SASToken, string Path)>> GetContentItems(string tenantId)
        {
            // Enumerate blobs
            var blobItems = await _container.ListBlobsSegmentedAsync($"{tenantId}/", null);
            var blobs = blobItems.Results.OfType<CloudBlockBlob>();

            // Build SAS with 15-minute expiration
            var adHocSAPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(5),
                Permissions = SharedAccessBlobPermissions.Read
            };

            return blobs
                .Select(item => {
                    // Generate the shared access signature on the blob, setting the constraints directly on the signature.
                    var sasBlobToken = item.GetSharedAccessSignature(adHocSAPolicy);
                    return (item.Uri, sasBlobToken, item.Name);
                });
        }

        public IEnumerable<BaseContentModel> ItemsAsJekyllFile(IEnumerable<(Uri Url, string SASToken, string Path)> items)
            => items.Select(
                i => new JekyllContentModel
                {
                    Url = i.Url + i.SASToken,
                    Name = Path.GetFileName(i.Path),
                });

        public async Task<IEnumerable<BaseContentModel>> ItemsAsJsonData(IEnumerable<(Uri Url, string SASToken, string Path)> items, string tenantId)
        {
            var tasks = (
                _noteTable.ScanAsync<NoteEntity>(tenantId),
                _dataTable.ScanAsync<DataEntity>(tenantId));

            await Task.WhenAll(tasks.Item1, tasks.Item2);
            var (notes, data) = (tasks.Item1.Result, tasks.Item2.Result);
            
            return (
                    from item in items
                        join note in notes
                            on item.Url.ToString() equals note.PublishedBlobUri
                        join d in data
                            on note.RowKey equals d.NoteId into dataItems
                    select (item, note, dataItems))
                .Select(
                    x =>  new JsonContentModel
                    {
                        Url = x.item.Url + x.item.SASToken,
                        Slug = x.note.Slug,
                        Title = x.note.Title,
                        Data = x.dataItems
                            .ToDictionary(
                                keySelector: d => d.Name.ToLower(),
                                elementSelector: d => d.Value),
                    });
        }
    }

    public class BaseContentModel
    {
        public string Url { get; set; } = string.Empty;
    }

    public class JekyllContentModel : BaseContentModel
    {
        public string Name { get; set; } = string.Empty;
    }

    public class JsonContentModel : BaseContentModel
    {
        public string? Slug { get; set; }
        public string? Title { get; set; }
        public Dictionary<string, string?> Data { get; set; } = new Dictionary<string, string?>();
    }
}
