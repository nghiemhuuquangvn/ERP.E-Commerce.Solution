using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Ocelot.API.Authorization;

/// <summary>
/// Token validation at the edge — the same rules as every service (ErpJwtBearerOptions:
/// RS256 via Identity's JWKS, exact issuer/audience, revocation check), so a forged or
/// revoked token is stopped before it is routed. Services validate again themselves.
/// </summary>
public static class GatewayJwtBearer
{
    public static void Configure(JwtBearerOptions options, IConfiguration configuration)
    {
        ErpJwtBearerOptions.Configure(options, configuration);
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(60);
    }
}
