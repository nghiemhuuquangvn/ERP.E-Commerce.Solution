using Microsoft.Extensions.Configuration;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.Interfaces.External;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace ERP.Identity.Infrastructure.ExternalAuthLogin
{
    public sealed class MicrosoftAuthProvider : IExternalAuthProvider
    {
        public string Provider => "Microsoft";
        private readonly IConfiguration _configuration;
        private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
        public MicrosoftAuthProvider(IConfiguration configuration)
        {
            _configuration = configuration;

            var authority = _configuration["Authentication:Microsoft:Authority"];
            var metadataAddress = $"{authority}/v2.0/.well-known/openid-configuration";
            _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(metadataAddress, new OpenIdConnectConfigurationRetriever());
        }
        public async Task<ExternalLoginDto.ExternalLoginResponse> VerifyAsync(ExternalLoginDto.ExternalLoginRequest request, CancellationToken cancellationToken = default)
        {
            var openIdConfig = await _configurationManager.GetConfigurationAsync(cancellationToken);
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidIssuer = _configuration["Authentication:Microsoft:Authority"],
                ValidAudience = _configuration["Authentication:Microsoft:ClientId"],
                IssuerSigningKeys = openIdConfig.SigningKeys,

                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true
            };
            try
            {
                var principal = tokenHandler.ValidateToken(request.IdToken, validationParameters, out var validatedToken);

                var providerUserId = principal.FindFirst("oid")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value; // Try 'oid' claim first, then fallback to NameIdentifier
                if (string.IsNullOrEmpty(providerUserId))
                {
                    throw new UnauthorizedAccessException("Invalid Microsoft ID Token: Missing user ID");
                }

                var email = principal.FindFirst("preferred_username")?.Value ?? principal.FindFirst(ClaimTypes.Email)?.Value; // Try 'preferred_username' claim first, then fallback to Email claim
                var name = principal.FindFirst("name")?.Value;

                return new ExternalLoginDto.ExternalLoginResponse(
                    Provider: Provider,
                    ProviderKey: providerUserId,
                    Email: email ?? string.Empty,
                    EmailVerified: true, // Microsoft doesn't provide a separate email verified claim, so assume it's verified
                    FullName: name ?? string.Empty);
            }
            catch (Exception)
            {
                throw new UnauthorizedAccessException("Invalid Microsoft ID Token");
            }
        }
    }
}