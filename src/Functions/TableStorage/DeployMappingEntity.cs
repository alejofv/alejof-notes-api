using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Functions.TableStorage
{
    ///
    /// <summary>PartitionKey: "deploy". RowKey: tenantId</summary>
    ///
    public class DeployMappingEntity : TableEntity
    {
        public const string TableName = "Auth0Mappings";
        public const string TenantKey = "deploy";

        public string NetlifySite { get; set; }
    }
}
