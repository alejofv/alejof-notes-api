using Microsoft.Extensions.Logging;

namespace Alejof.Notes.Functions.Infrastructure
{
    public interface IFunction
    {
        ILogger Log { get; set; }
        Settings.FunctionSettings Settings { get; set; }
    }

    public interface IAuthorizedFunction : IFunction
    {
        Auth.AuthContext AuthContext { get; set; }
    }
    
    public abstract class BaseFunction : IAuthorizedFunction
    {
        public ILogger Log { get; set; }
        public Settings.FunctionSettings Settings { get; set; }
        public Auth.AuthContext AuthContext { get; set; }
    }
}
