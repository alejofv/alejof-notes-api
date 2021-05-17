#nullable enable

using System.Threading.Tasks;
using MediatR;

namespace Alejof.Notes.Extensions
{
    public static class MediatorExtensions
    {
        public static async Task<TResponse> Send<TResponse>(this IMediator mediator, IRequest<TResponse> request, Auth.Identity identity)
        {
            var result = await mediator.Send<TResponse>(request);

            if (request is Handlers.IAuditableRequest auditableRequest)
            {
                await mediator.Publish(
                    new Handlers.Audit.Notification(identity, auditableRequest, result as Handlers.ActionResponse));
            }

            return result;
        }
    }
}
