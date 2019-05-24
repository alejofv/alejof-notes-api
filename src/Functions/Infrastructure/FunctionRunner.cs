using System;
using System.Threading.Tasks;
using Alejof.Notes.Functions.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Alejof.Notes.Functions.Infrastructure
{
    public class HttpFunctionRunner<TFunction>
        where TFunction : IFunction, new()
    {
        public Settings.FunctionSettings Settings { get; private set; }
        public ILogger Log { get; private set; }

        public HttpFunctionRunner(ILogger log, Settings.FunctionSettings settings = null)
        {
            Settings = settings ?? Notes.Settings.Factory.Build();
            Log = log;
        }

        public AuthenticatedHttpFunctionRunner<TFunction> WithAuthentication(HttpRequest req)
        {
            return new AuthenticatedHttpFunctionRunner<TFunction>(this, req);
        }
        
        public async Task<TResult> ExecuteAsync<TResult>(Func<TFunction, Task<TResult>> func)
        {
            var impl = new TFunction
            {
                Log = Log,
                Settings = Settings,
            };

            var data = await func(impl)
                .ConfigureAwait(false);

            return data;
        }
    }

    public class AuthenticatedHttpFunctionRunner<TFunction>
        where TFunction : IFunction, new()
    {
        public Settings.FunctionSettings Settings { get; private set; }
        public ILogger Log { get; private set; }
        private readonly HttpRequest _req;

        private const string LocalEnvName = "local";

        public AuthenticatedHttpFunctionRunner(HttpFunctionRunner<TFunction> originalRunner, HttpRequest req)
        {
            Log = originalRunner.Log;
            Settings = originalRunner.Settings;
            
            _req = req;
        }

        public async Task<(TResult, UnauthorizedResult)> ExecuteAsync<TResult>(Func<TFunction, Task<TResult>> func)
        {
            // MULTI-TENANT AUTH:

            // Authorization should return an AuthContext object if token is valid
            var tenantId = _req.GetTenantId();

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                Log.LogWarning("Tenant-Id header not found");
                return (default(TResult), new UnauthorizedResult());
            }
            
            Auth.AuthContext context;
            if (Settings.FunctionEnvironment != LocalEnvName)
            {
                context = await _req.AuthenticateAsync(tenantId, Log, Settings);
                if (context == null)
                    return (default(TResult), new UnauthorizedResult());
            }
            else
            {
                context = AuthContext.Local(tenantId);
            }

            // Set the context on the IFunction property, same fashion as Log and Settings

            var impl = new TFunction
            {
                Log = Log,
                Settings = Settings,
            };
            
            if (impl is IAuthorizedFunction)
                (impl as IAuthorizedFunction).AuthContext = context;

            var data = await func(impl)
                .ConfigureAwait(false);

            return (data, null);
        }
    }
    
    public static class HttpRunner
    {
        public static HttpFunctionRunner<TFunction> For<TFunction>(ILogger log) where TFunction : IFunction, new() => new HttpFunctionRunner<TFunction>(log);
    }
}
