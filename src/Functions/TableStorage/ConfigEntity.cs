using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Functions.TableStorage
{
    ///
    /// <summary>PartitionKey: tenantId. RowKey: configKey</summary>
    ///
    public class ConfigEntity : TableEntity
    {
        public const string TableName = "NoteAppConfigs";
        public const string DeployKey = "deploy-signal";
        public const string FormatKey = "format";

        public string Value { get; set; }
    }
}