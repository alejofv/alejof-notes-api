#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Alejof.Notes.Storage
{
    public static class CloudBlobExtensions
    {
        public static async Task<string> UploadAsync(this CloudBlobContainer container, string content, string filename)
        {
            await container.CreateIfNotExistsAsync();

            var blob = container.GetBlockBlobReference(filename.ToLowerInvariant());
            await blob.UploadTextAsync(content);

            return blob.Uri.ToString();
        }
        
        public static async Task<string> DownloadAsync(this CloudBlobContainer container, string uri)
        {
            await container.CreateIfNotExistsAsync();

            var blob = await container.ServiceClient.GetBlobReferenceFromServerAsync(new Uri(uri));

            using (var sm = new MemoryStream())
            {
                await blob.DownloadToStreamAsync(sm);
                return Encoding.UTF8.GetString(sm.ToArray());
            }
        }
        
        public static async Task DeleteAsync(this CloudBlobContainer container, string uri)
        {
            await container.CreateIfNotExistsAsync();
            
            var blob = await container.ServiceClient.GetBlobReferenceFromServerAsync(new Uri(uri));
            await blob.DeleteIfExistsAsync();
        }
    }
}
