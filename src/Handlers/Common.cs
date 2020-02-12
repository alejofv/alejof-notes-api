namespace Alejof.Notes.Handlers
{
    public abstract class BaseRequest
    {
        public string TenantId { get; set; }
    }

    public class ActionResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        public static ActionResponse Ok => new ActionResponse { Success = true };
    }
}