using Asp.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Shared.Versioning;

/// <summary>
/// Real API versioning (Asp.Versioning.Mvc), shared so every service configures it
/// identically instead of hand-repeating the same options 15 times. Backs the
/// `api/v{version:apiVersion}/...` route template convention used across the platform
/// (replaces the previous inconsistent mix of a hardcoded "v1" literal, or no version
/// segment at all, per service — see docs/architecture — this is not a cosmetic
/// change, `{version:apiVersion}` is a real route constraint this call registers).
/// UrlSegmentApiVersionReader + the default ApiVersion(1,0).ToString() formatting
/// ("1", not "1.0") means `v{version:apiVersion}` renders as "v1" in the URL —
/// identical text to what every already-versioned controller already produced.
/// </summary>
public static class ApiVersioningExtensions
{
    public static IServiceCollection AddErpApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });

        return services;
    }
}
