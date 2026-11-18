using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Shared.Extensions;

/// <summary>
/// One CORS rule for every service (security review 2026-11, S4a, SEC-M14; docs/architecture/browser-security.md):
/// the SPAs call the API on their OWN host (nginx proxies /api), and the mobile apps are native - neither needs a
/// cross-origin grant. Only the origins listed in <c>Cors:AllowedOrigins</c> are allowed (none by default); there is no
/// "allow any origin" any more (<c>NoOpenCorsTests</c>).
/// </summary>
public static class CorsExtensions
{
    public const string PolicyName = "ErpCors";
    public const string AllowedOriginsKey = "Cors:AllowedOrigins";

    public static IServiceCollection AddErpCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection(AllowedOriginsKey).Get<string[]>()?
            .Where(o => !string.IsNullOrWhiteSpace(o) && o.Trim() != "*")
            .Select(o => o.Trim().TrimEnd('/'))
            .ToArray() ?? [];

        services.AddCors(options => options.AddPolicy(PolicyName, policy =>
        {
            if (origins.Length > 0)
                policy.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader();
            // else: no origin is allowed - same-origin calls never need CORS.
        }));
        return services;
    }

    public static IApplicationBuilder UseErpCors(this IApplicationBuilder app) => app.UseCors(PolicyName);
}
