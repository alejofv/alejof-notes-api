using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace Alejof.Notes.Functions.TableStorage
{
    ///
    /// <summary>PartitionKey: "tenant". RowKey: tenantId</summary>
    ///
    public class TenantEntity : TableEntity
    {
        public const string TableName = "Auth0Mappings";
        public const string DefaultKey = "tenant";

        public string ClientID { get; set; }
        public string Domain { get; set; }
    }
}
