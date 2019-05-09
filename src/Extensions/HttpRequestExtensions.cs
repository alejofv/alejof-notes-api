using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Newtonsoft.Json;

namespace Alejof.Notes.Extensions
{
    public static class HttpRequestExtensions
    {
        public static async Task<T> GetJsonBodyAsAsync<T>(this HttpRequest req)
        {
            var content = await req.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(content);
        }
    }
}
