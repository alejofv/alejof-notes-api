#nullable enable

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Alejof.Notes.Handlers.Auth
{
    public class Auth0TokenValidator
    {
        private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
        private readonly string _issuer;
        private readonly string? _audience;
        
        internal Auth0TokenValidator(string domain, string? clientId)
        {
            _issuer = $"https://{domain}/";
            _audience = clientId;

            var documentRetriever = new HttpDocumentRetriever { RequireHttps = true };
            _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{_issuer}.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                documentRetriever);
        }

        public async Task<(ClaimsPrincipal?, string?)> ValidateTokenAsync(string token)
        {
            var config = await _configurationManager.GetConfigurationAsync(CancellationToken.None);

            var validationParameter = new TokenValidationParameters
            {
                RequireSignedTokens = true,
                ValidAudience = _audience,
                ValidateAudience = !(string.IsNullOrWhiteSpace(_audience)),
                ValidIssuer = _issuer,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                IssuerSigningKeys = config.SigningKeys
            };

            ClaimsPrincipal? result = null;
            var tries = 0;

            while (result == null && tries <= 1)
            {
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    result = handler.ValidateToken(token, validationParameter, out var jwt);
                }
                catch (SecurityTokenSignatureKeyNotFoundException)
                {
                    // This exception is thrown if the signature key of the JWT could not be found.
                    // This could be the case when the issuer changed its signing keys, so we trigger a 
                    // refresh and retry validation.
                    _configurationManager.RequestRefresh();
                    tries++;
                }
                catch (SecurityTokenException ex)
                {
                    return (null, ex.Message);
                }
            }

            return (result, null);
        }
    }
}