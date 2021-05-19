#nullable enable

using MediatR;

namespace Alejof.Notes.Handlers
{
    public enum PublishFormat
    {
        Plain,
        FrontMatter,
    }

    public abstract class BaseRequest
    {
        public string TenantId { get; set; } = string.Empty;
    }

    public interface IAuditableRequest
    {
        object AuditRecord { get; }
    }

    public abstract class BaseActionRequest : BaseRequest, IRequest<ActionResponse>
    {
        
    }

    public class ActionResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }

        public static ActionResponse Ok => new ActionResponse { Success = true };
    }
}
