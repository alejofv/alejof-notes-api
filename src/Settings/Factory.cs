using System;

namespace Alejof.Notes.Settings
{
    public class Factory
    {
        public static FunctionSettings Build()
        {
            var getTokenSetting = GetPrefixedSettingFunc<TokenSettings>();
            
            return new FunctionSettings
            {
                StorageConnectionString = GetSetting("AzureWebJobsStorage"),
                TokenSettings = new TokenSettings
                {
                    KeyModulus = getTokenSetting("KeyModulus"),
                    KeyExponent = getTokenSetting("KeyExponent"),
                    ValidIssuer = getTokenSetting("ValidIssuer"),
                }
            };
        }

        private static string GetSetting(string name) =>
            Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);

        private static Func<string, string> GetPrefixedSettingFunc<T>() =>
            name => Environment.GetEnvironmentVariable($"{typeof(T).Name}_{name}", EnvironmentVariableTarget.Process);
    }
}
