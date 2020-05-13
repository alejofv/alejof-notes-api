#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Alejof.Notes.Storage;
using MediatR;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace Alejof.Notes.Handlers
{
    public static class Audit
    {
        public class Notification : INotification
        {
            public Auth.Identity Identity { get; private set; }
            public IAuditableRequest Request { get; private set; }
            public ActionResponse Result { get; private set; }

            public Notification(Auth.Identity identity, IAuditableRequest request, ActionResponse result)
            {
                Identity = identity;
                Request = request;
                Result = result;
            }
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
                var entity = AuditLogEntity
                    .New(notification.Identity.TenantId);

                var requestType = notification.Request.GetType();
                
                entity.Action = requestType.DeclaringType?.Name ?? requestType.FullName;
                entity.Request = JsonConvert.SerializeObject(notification.Request.AuditRecord);
                entity.Response = notification.Result.Success ? "OK" : notification.Result.Message;

                await _logTable.InsertAsync(entity);
            }
        }
    }
}
