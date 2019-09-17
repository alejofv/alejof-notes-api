using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alejof.Notes.Extensions;
using Alejof.Notes.Functions;
using Alejof.Notes.Functions.Mapping;
using Alejof.Notes.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Alejof.Notes
{
    public static class AzureFunctions
    {
        [FunctionName("NotesGetAll")]
        public static async Task<IActionResult> GetNotesFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes")] HttpRequest req, ILogger log)
        {                
            log.LogInformation($"C# Http trigger function executed: {nameof(GetNotesFunction)}");
            
            bool getBoolEntry(IDictionary<string, string> dict, string param) =>
                dict.TryGetValue(param, out var value) && bool.TryParse(value, out var boolValue) ?
                    boolValue : false;

            var queryParams = req.GetQueryParameterDictionary();
            
            var published = getBoolEntry(queryParams, "published");
            var preserve = getBoolEntry(queryParams, "preserveSources");

            return await HttpRunner.For<NotesFunction>(log)
                .WithAuthentication(req)
                .ExecuteAsync(f => f.GetNotes(published, preserve))
                .AsIActionResult();
        }

        [FunctionName("NotesGet")]
        public static async Task<IActionResult> GetNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(GetNoteFunction)}");

            return await HttpRunner.For<NotesFunction>(log)
                .WithAuthentication(req)
                .ExecuteAsync(f => f.GetNote(id))
                .AsIActionResult();
        }

        [FunctionName("NotesCreate")]
        public static async Task<IActionResult> CreateNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "notes")] HttpRequest req, ILogger log)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(CreateNoteFunction)}");

            var note = await req.GetJsonBodyAsAsync<Models.Note>();
            if (note == null)
                return new BadRequestResult();

            var format = req.GetQueryParameterDictionary()
                .TryGetValue("format", out var formatValue) ? formatValue : "md";

            return await HttpRunner.For<NotesFunction>(log)
                .WithAuthentication(req)
                .ExecuteAsync(f => f.CreateNote(note, format))
                .AsIActionResult();
        }

        [FunctionName("NotesEdit")]
        public static async Task<IActionResult> EditNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "notes/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(EditNoteFunction)}");

            var note = await req.GetJsonBodyAsAsync<Models.Note>();
            if (note == null)
                return new BadRequestResult();

            note.Id = id;

            var format = req.GetQueryParameterDictionary()
                .TryGetValue("format", out var formatValue) ? formatValue : "md";

            return await HttpRunner.For<NotesFunction>(log)
                .WithAuthentication(req)
                .ExecuteAsync(f => f.EditNote(note, format))
                .AsIActionResult();
        }

        [FunctionName("NotesDelete")]
        public static async Task<IActionResult> DeleteNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "notes/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(DeleteNoteFunction)}");

            return await HttpRunner.For<NotesFunction>(log)
                .WithAuthentication(req)
                .ExecuteAsync(f => f.DeleteNote(id))
                .AsIActionResult();
        }

        [FunctionName("MediaGetAll")]
        public static async Task<IActionResult> GetMediaFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "media")] HttpRequest req, ILogger log)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(GetMediaFunction)}");

            return await HttpRunner.For<MediaFunction>(log)
                .WithAuthentication(req)
                .ExecuteAsync(f => f.GetMedia())
                .AsIActionResult();
        }

        [FunctionName("MediaUpload")]
        public static async Task<IActionResult> UploadMediaFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "media")] HttpRequest req, ILogger log, IBinder binder,
            [Queue("media-thumbnail-signal")]IAsyncCollector<string> thumbnailSignalCollector)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(UploadMediaFunction)}");

            var header = (string)req.Headers["Notes-Media-Name"];
            if (string.IsNullOrEmpty(header))
                return new BadRequestResult();

            return await HttpRunner.For<MediaFunction>(log)
                .WithAuthentication(req)
                .ExecuteAsync(
                    async function => 
                    {
                        var name = header.AsMediaName();
                        var result = await function.CreateMedia(name);
                        if (result.Success)
                        {
                            var blobName = function.GetMediaPath(name);

                            // save in blob storage using dynamic function binding
                            using (var output = await binder.BindAsync<System.IO.Stream>(new BlobAttribute(blobName, System.IO.FileAccess.Write)))
                                await req.Body.CopyToAsync(output);

                            // send signal to queue
                            await thumbnailSignalCollector.AddAsync(blobName);
                        }

                        return result;
                    })
                .AsIActionResult();
        }

        [FunctionName("MediaDelete")]
        public static async Task<IActionResult> DeleteMediaFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "media/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(DeleteMediaFunction)}");

            return await HttpRunner.For<MediaFunction>(log)
                .WithAuthentication(req)
                .ExecuteAsync(f => f.DeleteMedia(id))
                .AsIActionResult();
        }

        [FunctionName("ContentGet")]
        public static async Task<IActionResult> GetContentFunction(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "content/{tenantId}")] HttpRequest req, ILogger log, string tenantId)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(GetContentFunction)}");

            return await HttpRunner.For<ContentFunction>(log)
                .ExecuteAsync(f => f.GetContent(tenantId))
                .AsIActionResult();
        }

        [FunctionName("Publish")]
        public static async Task<IActionResult> PublishNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "delete", Route = "publish/{id}")] HttpRequest req, ILogger log, string id,
            [Queue("netlify-deploy-signal")]IAsyncCollector<string> redeploySignalCollector)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(PublishFunction)}, method: {req.Method}");

            var publish = !string.Equals(req.Method, "delete", StringComparison.OrdinalIgnoreCase);

            return await HttpRunner.For<PublishFunction>(log)
                .WithAuthentication(req)
                .ExecuteAsync(
                    async function =>
                    {
                        var publishResult = await function.Publish(id, publish);

                        if (publishResult.Success)
                            await redeploySignalCollector.AddAsync(function.AuthContext.TenantId);

                        return publishResult;
                    })
                .AsIActionResult<Models.Result>(x => new OkResult());
        }
    }
}
