using System;
using System.Threading.Tasks;
using Alejof.Notes.Functions.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alejof.Notes.Functions.Infrastructure
{
    public class FunctionRunner<TFunction>
        where TFunction : IFunction, new()
    {
        protected readonly Settings.FunctionSettings _settings;
        private ILogger _log;
        private HttpRequest _req;

        private const string LocalEnvName = "local";

        public FunctionRunner()
        {
            this._settings = Settings.Factory.Build();
        }

        public FunctionRunner<TFunction> WithLogger(ILogger log)
        {
            this._log = log;
            return this;
        }

        public FunctionRunner<TFunction> WithAuthorizedRequest(HttpRequest req)
        {
            this._req = req;
            return this;
        }
        
        public async Task<(TResult, UnauthorizedResult)> ExecuteAsync<TResult>(Func<TFunction, Task<TResult>> func)
        {
            var logToUse = _log ?? NullLogger.Instance;

            // MULTI-TENANT AUTH:

            // Authorization should return an AuthContext object if token is valid
            Auth.AuthContext context = null;
            if (_req != null && _settings.FunctionEnvironment != LocalEnvName)
            {
                context = await _req.AuthenticateAsync(logToUse, _settings);
                if (context == null)
                    return (default(TResult), new UnauthorizedResult());    
            }
            
            // Set the context on the IFunction property, same fashion as Log and Settings

            var impl = new TFunction
            {
                Log = logToUse,
                Settings = _settings,
                AuthContext = context,
            };

            var data = await func(impl)
                .ConfigureAwait(false);

            return (data, null);
        }
    }
    
    public static class HttpRunner
    {
        public static FunctionRunner<TFunction> For<TFunction>() where TFunction : IFunction, new() => new FunctionRunner<TFunction>();
    }
}
