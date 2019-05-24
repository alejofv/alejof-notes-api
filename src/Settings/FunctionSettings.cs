namespace Alejof.Notes.Settings
{
    public class FunctionSettings
    {
        public string StorageConnectionString { get; set; }
        public string FunctionEnvironment { get; set; }
        
        // TODO: Remove
        public string ContentSiteName { get; set; }
        // TODO: Remove
        public TokenSettings TokenSettings { get; set; }
    }
    
    public class TokenSettings
    {
        public string KeyModulus { get; set; }
        public string KeyExponent { get; set; }
        public string ValidIssuer { get; set; }
        public string ValidAudience { get; set; }
    }
}
