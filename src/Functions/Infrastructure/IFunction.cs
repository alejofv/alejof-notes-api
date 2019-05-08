using Microsoft.Extensions.Logging;

namespace Alejof.Notes.Functions.Infrastructure
{
    public interface IFunction
    {
        ILogger Log { get; set;}
        Settings.FunctionSettings Settings { get; set; }
    }
}
