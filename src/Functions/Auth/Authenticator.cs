using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Alejof.Notes.Extensions;
using Alejof.Notes.Functions.TableStorage;
using Flurl.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.WindowsAzure.Storage;

namespace Alejof.Notes.Functions.Auth
{
    public static class Authenticator
    {
        // Validate Auth0 token according to https://auth0.com/docs/api-auth/tutorials/verify-access-token
        public static async Task<AuthContext> AuthenticateAsync(this HttpRequest req, ILogger log, Settings.FunctionSettings settings)
        {                
            string authorization = req.Headers["Authorization"];
            if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Bearer"))
            {
                log.LogWarning("Authorization header not found or not valid (must be Bearer token)");
                return null;
            }

            var token = authorization.Substring("Bearer".Length).Trim();
            if (string.IsNullOrEmpty(token))
            {
                log.LogWarning("Token not found");
                return null;
            }

            // MULTI-TENANT AUTH:

            // 1. Require another header called tenant-id

            string tenantId = req.Headers["AlejoF-Tenant-Id"];
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                log.LogWarning("AlejoF-Tenant-Id header not found");
                return null;
            }

            // 2. Get auth0 domain and API id from tableStorage
            // -- Table: Auth0Mappings
            // -- PK:"tenant", RK:tenant-name, Auth0 domain, Client ID
            
            var storageAccount = CloudStorageAccount.Parse(settings.StorageConnectionString);
            var tableClient = storageAccount.CreateCloudTableClient();

            var table = tableClient.GetTableReference(AuthMappingEntity.TableName);
            var mapping = await table.RetrieveAsync<AuthMappingEntity>(AuthMappingEntity.TenantKey, tenantId);
            
            if (mapping == null)
            {
                log.LogWarning($"TenantId {tenantId} is not mapped");
                return null;
            }

            try
            {
                // 3. Get auth0 keys from https://{auth0 domain}/.well-known/jwks.json
                
                var jwks = await $"https://{mapping.Domain}/.well-known/jwks.json"
                    .GetJsonAsync();

                // 4. Validate token against tenant-specific params
                // Keys -> From jwks (n, e)
                
                var jwtKey = jwks.keys[0];
                var rsa = new RSACryptoServiceProvider();
                rsa.ImportParameters(
                    new RSAParameters()
                    {
                        Modulus = FromBase64Url(jwtKey.n),
                        Exponent = FromBase64Url(jwtKey.e)
                    });

                // issuer -> from table (Domain)
                // audience -> from table (ClientID) 

                var validationParameters = new TokenValidationParameters
                {
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,

                    ValidateIssuer = true,
                    ValidIssuer = $"https://{mapping.Domain}/",

                    ValidateAudience = true,
                    ValidAudience = mapping.ClientID,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new RsaSecurityKey(rsa),

                    ValidateLifetime = true,
                };

                var handler = new JwtSecurityTokenHandler();
                var claimsPrincipal = handler.ValidateToken(token, validationParameters, out var validatedToken);

                // 5. Return custom AuthContext object (or Claims list) to be set on the Function impl instance via interface Propoerty

                return BuildAuthContext(tenantId, claimsPrincipal);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Token validation failed");
                return null;
            }
        }

        private static byte[] FromBase64Url(string base64Url)
        {
            string padded = base64Url.Length % 4 == 0 ?
                base64Url
                : base64Url + "====".Substring(base64Url.Length % 4);
                
            string base64 = padded.Replace("_", "/").Replace("-", "+");
            
            return Convert.FromBase64String(base64);
        }
        
        private static AuthContext BuildAuthContext(string tenantId, ClaimsPrincipal principal)
        {
            // local function
            string findClaim(ClaimsPrincipal p, string type) => p.Claims
                .FirstOrDefault(c => string.Equals(c.Type, type, StringComparison.OrdinalIgnoreCase))
                ?.Value;

            // -- tenant (header)
            // -- username (jwt->nickname)
            // -- name (jwt->name)
            // -- email (jwt->email)

            return new AuthContext
            {
                TenantId = tenantId,
                Nickname = findClaim(principal, "nickname"),
                FullName = findClaim(principal, "name"),
                Email = findClaim(principal, "email"),
            };
        }
    }
}
