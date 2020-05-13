#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Alejof.Notes.Extensions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Alejof.Notes
{
    public class AdminFunctions
    {
        private readonly IMediator _mediator;

        public AdminFunctions(
            IMediator mediator)
        {
            this._mediator = mediator;
        }

        [FunctionName("AdminMigrate")]
        public async Task MigrateNotes(
            [QueueTrigger("notes-migrate-signal", Connection = "StorageConnectionString")] Handlers.Migration.Request message, ILogger log,
            [Queue("notes-publish-signal", Connection = "StorageConnectionString")]IAsyncCollector<PublishSignal> publishSignalCollector)
        {
            var result = await _mediator.Send(message);
            log.LogInformation($"Executed Migration for {message.TenantId}.");

            if (result.Success && result.PublishSignals.Any())
            {
                log.LogInformation($"Collecting Publish signals for {message.TenantId} ({result.PublishSignals.Count}).");

                foreach (var signal in result.PublishSignals)
                    await publishSignalCollector.AddAsync(
                        new PublishSignal
                        {
                            TenantId = message.TenantId, 
                            NoteId = signal.NoteId,
                            PublishDate = signal.PublishDate,
                        });
            }
        }

        [FunctionName("AdminPublish")]
        public async Task PublishNote(
            [QueueTrigger("notes-publish-signal", Connection = "StorageConnectionString")] PublishSignal message, ILogger log)
        {
            var request = new Handlers.PublishNote.Request
            {
                TenantId = message.TenantId,
                NoteId = message.NoteId,
                Date = message.PublishDate,
                Publish = true,
            };

            var result = await _mediator.Send(request);
            log.LogInformation($"Published note via queue signal. Result: {result.Success}, Message: {result.Message}.");
        }

        public class PublishSignal
        {
            public string TenantId { get; set; } = string.Empty;
            public string NoteId { get; set; } = string.Empty;
            public DateTime PublishDate { get; set; } = DateTime.UtcNow;
        }
    }
}
