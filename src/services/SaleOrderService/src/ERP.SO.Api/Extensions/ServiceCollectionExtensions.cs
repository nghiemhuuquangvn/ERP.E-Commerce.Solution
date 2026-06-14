using ERP.Shared.Authorization;

namespace ERP.SO.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMiddleware(this IServiceCollection services)
        {
            services.AddScoped<UserContext>(sp =>
            {
                var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
                var context = httpContextAccessor.HttpContext;

                if (context == null)
                    return new UserContext();

                var userContext = new UserContext();

                // Extract from JWT claims or custom headers
                if (context.Request.Headers.TryGetValue("X-User-Id", out var userId))
                    userContext.UserId = userId.ToString();

                if (context.Request.Headers.TryGetValue("X-User-Name", out var userName))
                    userContext.UserName = userName.ToString();

                if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantId))
                    userContext.TenantId = tenantId.ToString();

                if (context.Request.Headers.TryGetValue("X-Tenant-Name", out var tenantName))
                    userContext.TenantName = tenantName.ToString();

                // Parse permissions
                if (context.Request.Headers.TryGetValue("X-Permissions", out var permissions))
                {
                    var permissionArray = permissions.ToString()
                        .Split(',')
                        .Select(p => p.Trim())
                        .ToArray();
                    userContext.Permissions = permissionArray;
                }

                // Parse roles
                if (context.Request.Headers.TryGetValue("X-Roles", out var roles))
                {
                    var roleArray = roles.ToString()
                        .Split(',')
                        .Select(r => r.Trim())
                        .ToArray();
                    userContext.Roles = roleArray;
                }

                // Set expires at
                if (context.Request.Headers.TryGetValue("X-Token-Expires", out var expiresAt)
                    && long.TryParse(expiresAt.ToString(), out var timestamp))
                {
                    userContext.ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                }

                return userContext;
            });

            services.AddHttpContextAccessor();

            return services;
        }
    }
}
