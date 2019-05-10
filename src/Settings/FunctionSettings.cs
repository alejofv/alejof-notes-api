namespace Alejof.Notes.Settings
{
    public class FunctionSettings
    {
        public string StorageConnectionString { get; set; }
        public string FunctionEnvironment { get; set; }
        public string ContentSiteName { get; set; }
        
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
