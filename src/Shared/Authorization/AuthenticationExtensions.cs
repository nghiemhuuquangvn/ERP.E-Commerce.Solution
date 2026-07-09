using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace ERP.Shared.Authorization;

/// <summary>
/// Wires up real JWT signature/expiry validation for a downstream service, using the
/// same symmetric key/issuer/audience IdentityService issues tokens with (see
/// IdentityService's ERP.Identity.Api/Program.cs and TokenService.cs). Until this was
/// added, every non-Identity service trusted the Gateway's forwarded X-User-Id/
/// X-Tenant-Id/X-User-Roles/X-User-Permissions headers with zero cryptographic
/// verification — anyone with direct network access to a service, bypassing the
/// Gateway, could forge those headers and impersonate any user/tenant/role. Pair
/// this with JwtClaimsUserContextService (which reads the now-validated
/// HttpContext.User claims instead of headers) and call app.UseAuthentication()
/// before app.UseAuthorization() in the service's request pipeline.
/// </summary>
public static class AuthenticationExtensions
{
    public static IServiceCollection AddErpJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Configuration value 'Jwt:Key' is missing.");
        var issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Configuration value 'Jwt:Issuer' is missing.");
        var audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Configuration value 'Jwt:Audience' is missing.");

        services.AddAuthentication("Bearer")
            .AddJwtBearer("Bearer", options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };
            });

        return services;
    }
}
