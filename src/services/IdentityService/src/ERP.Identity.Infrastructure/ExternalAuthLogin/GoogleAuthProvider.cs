using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.Interfaces.External;
using static ERP.Identity.Application.DTOs.Auth.ExternalLoginDto;

namespace ERP.Identity.Infrastructure.ExternalAuthLogin
{
    public sealed class GoogleAuthProvider : IExternalAuthProvider
    {
        public string Provider => "Google";
        private readonly IConfiguration _configuration;

        public GoogleAuthProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<ExternalLoginResponse> VerifyAsync(ExternalLoginRequest request, CancellationToken cancellationToken = default)
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[]
                {
                    _configuration["Authentication:Google:ClientId"]
                }
            };

            GoogleJsonWebSignature.Payload payload;

            try
            {
                payload = await GoogleJsonWebSignature
                    .ValidateAsync(request.IdToken, settings);
            }
            catch (Exception)
            {
                throw new UnauthorizedAccessException("Invalid Google ID Token");
            }

            return new ExternalLoginResponse(
                Provider: Provider,
                ProviderKey: payload.Subject,
                Email: payload.Email,
                EmailVerified: payload.EmailVerified,
                FullName: payload.Name);
        }
    }
}