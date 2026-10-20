using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Ocelot.API.Authorization;
using Xunit;

namespace ERP.Gateway.UnitTests;

/// <summary>
/// The Gateway validates tokens with the same rules as every service (RS256 via JWKS, exact
/// issuer and audience) — before phase 4 it skipped issuer/audience and used the shared key.
/// </summary>
public class GatewayJwtBearerTests
{
    private static readonly RsaSecurityKey IdentityKey = new(RSA.Create(2048)) { KeyId = "identity" };

    private static JwtBearerOptions Options()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "ERP.Identity",
            ["Jwt:Audience"] = "ERP.Services",
            ["Jwt:Authority"] = "http://identityservice:5001",
        }).Build();
        var options = new JwtBearerOptions();
        GatewayJwtBearer.Configure(options, configuration);
        return options;
    }

    private static string Token(string issuer, string audience) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(new[] { new Claim("sub", Guid.NewGuid().ToString()) }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(IdentityKey, SecurityAlgorithms.RsaSha256)
        });

    private static async Task<bool> IsAcceptedAsync(string token)
    {
        var parameters = Options().TokenValidationParameters.Clone();
        // What JwtBearerHandler adds from the JWKS document.
        parameters.IssuerSigningKeys = parameters.IssuerSigningKeys.Append(IdentityKey).ToList();
        return (await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters)).IsValid;
    }

    [Fact]
    public async Task IdentityRs256Token_IsAccepted()
    {
        (await IsAcceptedAsync(Token("ERP.Identity", "ERP.Services"))).Should().BeTrue();
    }

    [Theory]
    [InlineData("urn:ERP.Identity", "ERP.Services")]
    [InlineData("ERP.Identity", "someone-else")]
    public async Task OtherIssuerOrAudience_IsRejectedAtTheEdge(string issuer, string audience)
    {
        (await IsAcceptedAsync(Token(issuer, audience))).Should().BeFalse();
    }

    [Fact]
    public void WithoutJwtKey_OnlyRs256IsAllowed_AndKeysComeFromJwks()
    {
        var options = Options();

        options.TokenValidationParameters.ValidAlgorithms.Should().Equal(SecurityAlgorithms.RsaSha256);
        options.TokenValidationParameters.ClockSkew.Should().Be(TimeSpan.FromSeconds(60));
        options.ConfigurationManager.Should().NotBeNull();
    }
}
