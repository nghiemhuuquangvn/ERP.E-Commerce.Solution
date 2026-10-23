using Microsoft.Extensions.Configuration;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.Interfaces.External;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using static ERP.Identity.Application.DTOs.Auth.ExternalLoginDto;

namespace ERP.Identity.Infrastructure.ExternalAuthLogin
{
    public sealed class FacebookAuthProvider : IExternalAuthProvider
    {
        public string Provider => "Facebook";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public FacebookAuthProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<ExternalLoginResponse> VerifyAsync(ExternalLoginRequest request, CancellationToken cancellationToken = default)
        {
            var appId = _configuration["Authentication:Facebook:AppId"]
                ?? throw new InvalidOperationException("Authentication:Facebook:AppId configuration is missing.");
            var appSecret = _configuration["Authentication:Facebook:AppSecret"]
                ?? throw new InvalidOperationException("Authentication:Facebook:AppSecret configuration is missing.");

            var client = _httpClientFactory.CreateClient();

            // 1) Debug the token to confirm it was issued for OUR app (the audience check
            // equivalent of Google/Microsoft's aud/client_id validation).
            var debugUrl = $"https://graph.facebook.com/debug_token?input_token={Uri.EscapeDataString(request.IdToken)}&access_token={Uri.EscapeDataString(appId)}|{Uri.EscapeDataString(appSecret)}";

            FacebookDebugTokenResponse? debugResponse;
            try
            {
                debugResponse = await client.GetFromJsonAsync<FacebookDebugTokenResponse>(debugUrl, cancellationToken);
            }
            catch (Exception)
            {
                throw new UnauthorizedAccessException("Invalid Facebook access token");
            }

            var debugAppId = debugResponse?.Data?.AppId;
            if (string.IsNullOrEmpty(debugAppId) || !string.Equals(debugAppId, appId, StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Facebook access token was not issued for this application");

            if (debugResponse?.Data?.IsValid != true)
                throw new UnauthorizedAccessException("Facebook access token is not valid");

            // 2) Fetch the profile
            var profileUrl = $"https://graph.facebook.com/me?fields=id,name,email&access_token={Uri.EscapeDataString(request.IdToken)}";

            FacebookProfileResponse? profile;
            try
            {
                profile = await client.GetFromJsonAsync<FacebookProfileResponse>(profileUrl, cancellationToken);
            }
            catch (Exception)
            {
                throw new UnauthorizedAccessException("Unable to retrieve Facebook profile");
            }

            if (profile == null || string.IsNullOrEmpty(profile.Id))
                throw new UnauthorizedAccessException("Invalid Facebook profile response");

            return new ExternalLoginResponse(
                Provider: Provider,
                ProviderKey: profile.Id,
                Email: profile.Email ?? string.Empty,
                // The Graph API says nothing about verification: having an address does not prove the user owns it, and
                // an unverified address must never be used to take over an existing account.
                EmailVerified: false,
                FullName: profile.Name);
        }

        private sealed class FacebookProfileResponse
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("email")]
            public string? Email { get; set; }
        }

        private sealed class FacebookDebugTokenResponse
        {
            [JsonPropertyName("data")]
            public FacebookDebugTokenData? Data { get; set; }
        }

        private sealed class FacebookDebugTokenData
        {
            [JsonPropertyName("app_id")]
            public string? AppId { get; set; }

            [JsonPropertyName("is_valid")]
            public bool IsValid { get; set; }
        }
    }
}
