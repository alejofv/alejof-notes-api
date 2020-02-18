#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Alejof.Notes.Storage;
using MediatR;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Handlers
{
    public static class Audit
    {
        public class Notification : INotification
        {
            public string TenantId { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Action { get; set; } = string.Empty;
            public ActionResponse? Result { get; set; }
        }

        public class Handler : INotificationHandler<Notification>
        {
            private readonly CloudTable _logTable;

            public Handler(CloudTableClient client)
            {
                _logTable = client.GetTableReference(AuditLogEntity.TableName);
            }
            
            public async Task Handle(Notification notification, CancellationToken cancellationToken)
            {
                await _logTable.CreateIfNotExistsAsync();
                
                var entity = AuditLogEntity
                    .New(notification.TenantId);

                entity.Email = notification.Email;
                entity.Action = notification.Action;
                entity.Message = notification.Result?.Success == true ? "OK" : notification.Result?.Message;

                await _logTable.InsertAsync(entity);
            }
        }
    }
}
