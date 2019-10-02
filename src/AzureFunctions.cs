using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alejof.Notes.Extensions;
using Alejof.Notes.Functions;
using Alejof.Notes.Functions.Mapping;
using Alejof.Notes.Infrastructure;
using Alejof.Notes.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Alejof.Notes
{
    public static class AzureFunctions
    {
        private static bool GetBoolEntry(IDictionary<string, string> dict, string param) =>
            dict.TryGetValue(param, out var value) && bool.TryParse(value, out var boolValue) ? boolValue : false;
                            
        [FunctionName("NotesGetAll")]
        public static async Task<IActionResult> GetNotesFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes")] HttpRequest req, ILogger log)
        {                
            log.LogInformation($"C# Http trigger function executed: {nameof(GetNotesFunction)}");

            var published = GetBoolEntry(req.GetQueryParameterDictionary(), "published");

            return await FunctionRunner.For<NotesFunction>(log)
                .Authenticate(req)
                .AndRunAsync(f => f.GetNotes(published))
                .AsIActionResult();
        }

        [FunctionName("NotesGet")]
        public static async Task<IActionResult> GetNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(GetNoteFunction)}");

            var published = GetBoolEntry(req.GetQueryParameterDictionary(), "published");

            return await FunctionRunner.For<NotesFunction>(log)
                .Authenticate(req)
                .AndRunAsync(f => f.GetNote(id, published))
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

            return await FunctionRunner.For<NotesFunction>(log)
                .Authenticate(req)
                .AndRunAsync(f => f.CreateNote(note, format))
                .AsIActionResult();
        }

        [FunctionName("NotesEdit")]
        public static async Task<IActionResult> EditNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "notes/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(EditNoteFunction)}");

            var note = await req.GetJsonBodyAsAsync<Note>();
            if (note == null)
                return new BadRequestResult();

            note.Id = id;

            var queryParams = req.GetQueryParameterDictionary();
            var published = GetBoolEntry(queryParams, "published");
            var format = queryParams.TryGetValue("format", out var formatValue) ? formatValue : "md";

            return await FunctionRunner.For<NotesFunction>(log)
                .Authenticate(req)
                .AndRunAsync(f => f.EditNote(note, format, published))
                .AsIActionResult();
        }

        [FunctionName("NotesDelete")]
        public static async Task<IActionResult> DeleteNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "notes/{id}")] HttpRequest req, ILogger log, string id)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(DeleteNoteFunction)}");

            return await FunctionRunner.For<NotesFunction>(log)
                .Authenticate(req)
                .AndRunAsync(f => f.DeleteNote(id))
                .AsIActionResult();
        }

        [FunctionName("MediaGetAll")]
        public static async Task<IActionResult> GetMediaFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "media")] HttpRequest req, ILogger log)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(GetMediaFunction)}");

            return await FunctionRunner.For<MediaFunction>(log)
                .Authenticate(req)
                .AndRunAsync(f => f.GetMedia())
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

            return await FunctionRunner.For<MediaFunction>(log)
                .Authenticate(req)
                .AndRunAsync(
                    async function => 
                    {
                        var name = header.AsMediaName();
                        var result = await function.CreateMedia(name, req.Body);

                        if (result.Success)
                        {
                            var mediaPath = $"{MediaFunction.BlobContainerName}/{function.GetBlobName(name)}";
                            await thumbnailSignalCollector.AddAsync(mediaPath);
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

            return await FunctionRunner.For<MediaFunction>(log)
                .Authenticate(req)
                .AndRunAsync(f => f.DeleteMedia(id))
                .AsIActionResult();
        }

        [FunctionName("Publish")]
        public static async Task<IActionResult> PublishNoteFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", "delete", Route = "publish/{id}")] HttpRequest req, ILogger log, string id,
            [Queue("netlify-deploy-signal")]IAsyncCollector<string> deploySignalCollector)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(PublishFunction)}, method: {req.Method}");

            var publish = !string.Equals(req.Method, "delete", StringComparison.OrdinalIgnoreCase);

            return await FunctionRunner.For<PublishFunction>(log)
                .Authenticate(req)
                .AndRunAsync(
                    async function =>
                    {
                        var publishResult = await function.Publish(id, publish);

                        if (publishResult.Success)
                            await deploySignalCollector.AddAsync(function.AuthContext.TenantId);

                        return Result.Ok;
                    })
                .AsIActionResult(x => new OkResult());
        }

        [FunctionName("ContentGet")]
        public static async Task<IActionResult> GetContentFunction(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "content/{tenantId}")] HttpRequest req, ILogger log, string tenantId)
        {
            log.LogInformation($"C# Http trigger function executed: {nameof(GetContentFunction)}");

            return await FunctionRunner.For<ContentFunction>(log)
                .RunAsync(f => f.GetContent(tenantId))
                .AsIActionResult();
        }
    }
}
