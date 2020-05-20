#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alejof.Notes.Extensions;
using Alejof.Notes.Storage;
using AutoMapper;
using MediatR;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Alejof.Notes.Handlers.Content
{
    public class Request : BaseRequest, IRequest<Response>
    {
    }

    public class Response
    {
        public IReadOnlyCollection<ContentModel> ContentList { get; private set; }

        public Response(IEnumerable<ContentModel> contentEnumerable)
        {
            this.ContentList = contentEnumerable.ToList().AsReadOnly();
        }
    }

    public class Handler : IRequestHandler<Request, Response>
    {
        private readonly CloudBlobContainer _container;

        public Handler(
            CloudBlobClient blobClient)
        {
            this._container = blobClient.GetContainerReference(Blobs.PublishContainerName);
        }

        public async Task<Response> Handle(Request request, CancellationToken cancellationToken)
        {
            // Enumerate blobs
            var blobs = await _container.ListBlobsSegmentedAsync($"{request.TenantId}/", null);

            // Build SAS with 15-minute expiration
            var adHocSAPolicy = new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(5),
                Permissions = SharedAccessBlobPermissions.Read
            };

            return new Response(
                blobs.Results.OfType<CloudBlockBlob>().Select(b => BuildContentModel(b, adHocSAPolicy)));
        }

        public ContentModel BuildContentModel(CloudBlockBlob item, SharedAccessBlobPolicy policy)
        {
            // Generate the shared access signature on the blob, setting the constraints directly on the signature.
            var sasBlobToken = item.GetSharedAccessSignature(policy);
            return new ContentModel
            {
                Url = item.Uri + sasBlobToken,
                Name = System.IO.Path.GetFileName(item.Name),
            };
        }

    }
    public class ContentModel
    {
        public string Url { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

}
