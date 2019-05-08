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
                                        
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new RsaSecurityKey(rsa),

                    ValidateLifetime = true,
                    ValidateAudience = false,
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
