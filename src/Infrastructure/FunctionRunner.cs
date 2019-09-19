using System;
using System.Threading.Tasks;
using Alejof.Notes.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Alejof.Notes.Infrastructure
{
    public class FunctionRunner<TFunction>
        where TFunction : IFunction, new()
    {
        public Settings.FunctionSettings Settings { get; private set; }
        public ILogger Log { get; private set; }

        public FunctionRunner(ILogger log, Settings.FunctionSettings settings = null)
        {
            Log = log;
            Settings = settings ?? Notes.Settings.Factory.Build();
        }

        public FunctionRunnerWithAuthentication<TFunction> Authenticate(HttpRequest req) =>
            new FunctionRunnerWithAuthentication<TFunction>(this.Log, this.Settings, req);
        
        public async Task<TResult> RunAsync<TResult>(Func<TFunction, Task<TResult>> func)
        {
            var impl = new TFunction
            {
                Log = this.Log,
                Settings = this.Settings,
            };

            var data = await func(impl)
                .ConfigureAwait(false);

            return data;
        }
    }

    public class FunctionRunnerWithAuthentication<TFunction>
        where TFunction : IFunction, new()
    {
        public Settings.FunctionSettings Settings { get; private set; }
        public ILogger Log { get; private set; }
        private readonly HttpRequest _req;

        private const string LocalEnvName = "local";

        public FunctionRunnerWithAuthentication(ILogger log, Settings.FunctionSettings settings, HttpRequest req)
        {
            Log = log;
            Settings = settings;
            
            _req = req;
        }

        public async Task<(TResult, UnauthorizedResult)> AndRunAsync<TResult>(Func<TFunction, Task<TResult>> func)
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
    
    public static class FunctionRunner
    {
        public static FunctionRunner<TFunction> For<TFunction>(ILogger log) where TFunction : IFunction, new() => new FunctionRunner<TFunction>(log);
    }
}
