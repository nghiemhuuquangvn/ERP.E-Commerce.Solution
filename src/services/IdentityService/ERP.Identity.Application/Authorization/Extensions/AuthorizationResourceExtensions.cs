using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ERP.Identity.Api.Authorization.Extensions;

public static class AuthorizationResourceExtensions
{
    public static IReadOnlyList<string> GetRequiredPermissions(this object resource)
    {
        if (resource is not HttpContext httpContext)
            return Array.Empty<string>();

        var endpoint = httpContext.GetEndpoint();
        if (endpoint == null)
            return Array.Empty<string>();

        return endpoint.Metadata
            .GetOrderedMetadata<RequirePermissionAttribute>()
            .SelectMany(a => a.Permissions)
            .Distinct()
            .ToList();
    }
}