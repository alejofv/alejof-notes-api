using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Alejof.Notes.Functions.Auth
{
    public static class Authenticator
    {
        public static bool IsAuthenticated(this HttpRequest req, Settings.TokenSettings tokenSettings, ILogger log)
        {                
            string headerValue = req.Headers["Authorization"];
            if (string.IsNullOrWhiteSpace(headerValue) || !headerValue.StartsWith("Bearer"))
            {
                log.LogWarning("Authorization header not found or not valid (must be Bearer token)");
                return false;
            }

            var token = headerValue.Substring("Bearer".Length).Trim();
            if (string.IsNullOrEmpty(token))
            {
                log.LogWarning("Token not found");
                return false;
            }

            // TODO: MULTI-TENANT ARCHITECTURE:

            // 1. Require another header called tenant-id

            // 2. Get auth0 domain and API id from tableStorage
            // -- Table: Auth0Mappings
            // -- PK:"tenant", RK:tenant-name, Auth0 domain, Client ID

            // 3. Get auth0 keys from https://{auth0 domain}/.well-known/jwks.json

            // 4. Validate token against tenant-specific params

            // 5. Return custom AuthContext object (or Claims list) to be set on the Function impl instance via interface Propoerty
            // -- tenant (header)
            // -- username (jwt->nickname)
            // -- name (jwt->name)
            // -- email (jwt->email)

            try
            {
                var rsa = new RSACryptoServiceProvider();
                rsa.ImportParameters(
                    new RSAParameters()
                    {
                        Modulus = FromBase64Url(tokenSettings.KeyModulus),
                        Exponent = FromBase64Url(tokenSettings.KeyExponent)
                    });
                    
                var validationParameters = new TokenValidationParameters
                {
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    
                    ValidateIssuer = true,
                    ValidIssuer = tokenSettings.ValidIssuer,

                    ValidateAudience = true,
                    ValidAudience = tokenSettings.ValidAudience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new RsaSecurityKey(rsa),

                    ValidateLifetime = true,
                };

                var handler = new JwtSecurityTokenHandler();
                handler.ValidateToken(token, validationParameters, out var validatedToken);
                
                return true;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Token validation failed");
                return false;
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
    }
}
