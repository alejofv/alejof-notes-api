using System;

namespace Alejof.Notes.Settings
{
    public class Factory
    {
        public static FunctionSettings Build()
        {
            return new FunctionSettings
            {
                StorageConnectionString = GetSetting("AzureWebJobsStorage"),
            };
        }

        private static string GetSetting(string name) =>
            Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);

        private static Func<string, string> GetPrefixedSettingFunc<T>() =>
            name => Environment.GetEnvironmentVariable($"{typeof(T).Name}_{name}", EnvironmentVariableTarget.Process);
    }
}
