using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.Interfaces.Repositories;
using ERP.Identity.Domain.Entities;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Identity.Infrastructure.ExternalAuthLogin
{
    /// <summary>
    /// Twitter/X login uses a stateful OAuth2 Authorization Code + PKCE redirect flow —
    /// unlike Google/Microsoft/Facebook's single-shot id-token verify, so it is not an
    /// IExternalAuthProvider. AuthService.TwitterCallbackAsync consumes this directly.
    /// </summary>
    public class TwitterOAuthService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IUnitOfWork _unitOfWork;

        public TwitterOAuthService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IUnitOfWork unitOfWork)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _unitOfWork = unitOfWork;
        }

        public async Task<TwitterAuthorizeUrlResponse> GetAuthorizeUrlAsync(string tenantCode, CancellationToken ct)
        {
            var clientId = _configuration["Authentication:Twitter:ClientId"]
                ?? throw new InvalidOperationException("Authentication:Twitter:ClientId configuration is missing.");
            var redirectUri = _configuration["Authentication:Twitter:RedirectUri"]
                ?? throw new InvalidOperationException("Authentication:Twitter:RedirectUri configuration is missing.");

            var oauthState = OAuthState.Create(tenantCode, expiryMinutes: 10, out var codeChallenge);
            await _unitOfWork.OAuthStates.AddAsync(oauthState, ct);

            var authorizeUrl = "https://twitter.com/i/oauth2/authorize" +
                $"?response_type=code" +
                $"&client_id={Uri.EscapeDataString(clientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&scope={Uri.EscapeDataString("tweet.read users.read")}" +
                $"&state={Uri.EscapeDataString(oauthState.State)}" +
                $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
                $"&code_challenge_method=S256";

            return new TwitterAuthorizeUrlResponse(authorizeUrl, oauthState.State);
        }

        public async Task<TwitterCallbackResult> HandleCallbackAsync(string code, string state, CancellationToken ct)
        {
            var oauthState = await _unitOfWork.OAuthStates.GetByStateAsync(state, ct)
                ?? throw new UnauthorizedAccessException("Invalid or unknown OAuth state");

            if (oauthState.IsExpired)
                throw new UnauthorizedAccessException("OAuth state has expired");

            var clientId = _configuration["Authentication:Twitter:ClientId"]
                ?? throw new InvalidOperationException("Authentication:Twitter:ClientId configuration is missing.");
            var clientSecret = _configuration["Authentication:Twitter:ClientSecret"]
                ?? throw new InvalidOperationException("Authentication:Twitter:ClientSecret configuration is missing.");
            var redirectUri = _configuration["Authentication:Twitter:RedirectUri"]
                ?? throw new InvalidOperationException("Authentication:Twitter:RedirectUri configuration is missing.");

            var client = _httpClientFactory.CreateClient();

            var basicAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));

            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.twitter.com/2/oauth2/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["grant_type"] = "authorization_code",
                    ["client_id"] = clientId,
                    ["redirect_uri"] = redirectUri,
                    ["code_verifier"] = oauthState.CodeVerifier
                })
            };
            tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

            TwitterTokenResponse? tokenResponse;
            try
            {
                var tokenHttpResponse = await client.SendAsync(tokenRequest, ct);
                tokenHttpResponse.EnsureSuccessStatusCode();
                tokenResponse = await tokenHttpResponse.Content.ReadFromJsonAsync<TwitterTokenResponse>(cancellationToken: ct);
            }
            catch (Exception)
            {
                throw new UnauthorizedAccessException("Failed to exchange Twitter authorization code for an access token");
            }

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
                throw new UnauthorizedAccessException("Twitter token response did not contain an access token");

            using var meRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.twitter.com/2/users/me");
            meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);

            TwitterUsersMeResponse? meResponse;
            try
            {
                var meHttpResponse = await client.SendAsync(meRequest, ct);
                meHttpResponse.EnsureSuccessStatusCode();
                meResponse = await meHttpResponse.Content.ReadFromJsonAsync<TwitterUsersMeResponse>(cancellationToken: ct);
            }
            catch (Exception)
            {
                throw new UnauthorizedAccessException("Failed to retrieve Twitter user profile");
            }

            if (meResponse?.Data == null || string.IsNullOrEmpty(meResponse.Data.Id))
                throw new UnauthorizedAccessException("Invalid Twitter user profile response");

            return new TwitterCallbackResult(
                ProviderKey: meResponse.Data.Id,
                Email: null, // Twitter's API does not reliably expose email
                TenantCode: oauthState.TenantCode);
        }

        private sealed class TwitterTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }
        }

        private sealed class TwitterUsersMeResponse
        {
            [JsonPropertyName("data")]
            public TwitterUserData? Data { get; set; }
        }

        private sealed class TwitterUserData
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("username")]
            public string? Username { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }
        }
    }

    public record TwitterCallbackResult(string ProviderKey, string? Email, string TenantCode);
}
