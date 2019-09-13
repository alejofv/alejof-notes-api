namespace Alejof.Notes.Auth
{
    public class AuthContext
    {        
        public string TenantId { get; set; }
        public string Nickname { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        
        public static AuthContext Local(string tenantId) => new AuthContext
        {
            TenantId = tenantId,
            Nickname = "local",
            FullName = "local user",
            Email = "user@local.com",
        };
    }
}
