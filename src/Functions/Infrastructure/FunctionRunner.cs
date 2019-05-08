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

        public FunctionRunner()
        {
            this._settings = Settings.Factory.Build();
        }

        public FunctionRunner<TFunction> WithLogger(ILogger log)
        {
            this._log = log;
            return this;
        }

        public FunctionRunner<TFunction> WithAuth(HttpRequest req)
        {
            this._req = req;
            return this;
        }

        public async Task<IActionResult> RunAsync(Func<TFunction, Task<IActionResult>> func)
        {
            var logToUse = _log ?? NullLogger.Instance;
            
            if (_req != null && !_req.IsAuthenticated(_settings.TokenSettings, logToUse))
                return new UnauthorizedResult();

            var impl = new TFunction
            {
                Log = logToUse,
                Settings = _settings,
            };

            return await func(impl)
                .ConfigureAwait(false);
        }
    }
    
    public static class FunctionRunner
    {
        public static FunctionRunner<TFunction> For<TFunction>() where TFunction : IFunction, new() => new FunctionRunner<TFunction>();
    }
}
