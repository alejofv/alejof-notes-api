using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Alejof.Notes.Extensions
{
    public static class CloudBlobExtensions
    {
        public static Task<string> UploadAsync(this CloudBlobContainer container, string content, string filename) => 
            UploadAsync(container, new MemoryStream(Encoding.UTF8.GetBytes(content)), filename);

        public static async Task<string> UploadAsync(this CloudBlobContainer container, Stream data, string filename)
        {
            var blob = container.GetBlockBlobReference(filename.ToLowerInvariant());
            await blob.UploadFromStreamAsync(data);

            return blob.Uri.ToString();
        }
        
        public static async Task<string> DownloadAsync(this CloudBlobContainer container, string uri)
        {
            var blob = await container.ServiceClient.GetBlobReferenceFromServerAsync(new Uri(uri));

            using (var sm = new MemoryStream())
            {
                await blob.DownloadToStreamAsync(sm);
                return Encoding.UTF8.GetString(sm.ToArray());
            }
        }
        
        public static async Task DeleteAsync(this CloudBlobContainer container, string uri)
        {
            var blob = await container.ServiceClient.GetBlobReferenceFromServerAsync(new Uri(uri));
            await blob.DeleteIfExistsAsync();
        }
    }
}
