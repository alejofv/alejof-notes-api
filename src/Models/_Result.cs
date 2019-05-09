namespace Alejof.Notes.Models
{
    public class Result
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        // Factory methods
        public static Result Ok => new Result { Success = true };
    }
    
    public class Result<T> : Result
    {
        public T Data { get; set; }
    }
    
    public static class ResultFactory
    {
        public static Result AsFailedResult(this string message) => new Result { Success = false, Message = message };
        public static Result<T> AsOkResult<T>(this T data) => new Result<T> { Success = true, Data = data };
        public static Result<T> AsFailedResult<T>(this T data, string message) => new Result<T> { Success = false, Message = message, Data = data };
    }
}
