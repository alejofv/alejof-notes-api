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

        public async Task<IActionResult> ExecuteAsync<TResult>(Func<TFunction, Task<TResult>> func)
        {
            var logToUse = _log ?? NullLogger.Instance;
            
            if (_req != null && _settings.FunctionEnvironment != LocalEnvName && !_req.IsAuthenticated(_settings.TokenSettings, logToUse))
                return new UnauthorizedResult();

            var impl = new TFunction
            {
                Log = logToUse,
                Settings = _settings,
            };

            var data = await func(impl)
                .ConfigureAwait(false);

            return new OkObjectResult(data);
        }
    }
    
    public static class HttpRunner
    {
        public static FunctionRunner<TFunction> For<TFunction>() where TFunction : IFunction, new() => new FunctionRunner<TFunction>();
    }
}
