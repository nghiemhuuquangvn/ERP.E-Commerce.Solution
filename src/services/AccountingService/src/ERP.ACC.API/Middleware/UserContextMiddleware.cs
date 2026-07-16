using ERP.Shared.Authorization;

namespace ERP.ACC.API.Middleware
{
    public static class UserContextMiddlewareExtensions
    {
        public static IApplicationBuilder UseUserContextHeaderMiddleware(this IApplicationBuilder app)
        {
            return app.UseMiddleware<UserContextHeaderMiddleware>();
        }
    }

    /// <summary>
    /// Populates the request-scoped UserContext from the now-validated JWT claims on
    /// context.User (populated by AddErpJwtAuthentication + app.UseAuthentication(),
    /// which must run before this middleware) and gates on real authentication —
    /// previously this trusted the Gateway's forwarded X-User-*/X-Tenant-* headers
    /// directly, which anyone with direct network access to this service (bypassing
    /// the Gateway) could forge to impersonate any user/tenant/role.
    /// </summary>
    public class UserContextHeaderMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<UserContextHeaderMiddleware> _logger;

        public UserContextHeaderMiddleware(RequestDelegate next, ILogger<UserContextHeaderMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        private static readonly string[] _openPaths = ["/health", "/swagger", "/openapi"];

        public async Task InvokeAsync(HttpContext context, UserContext userContext, IUserContextService userContextService)
        {
            var path = context.Request.Path.Value ?? "";
            if (_openPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }

            var authenticated = userContextService.GetCurrentUser();
            userContext.UserId = authenticated.UserId;
            userContext.UserName = authenticated.UserName;
            userContext.TenantId = authenticated.TenantId;
            userContext.TenantName = authenticated.TenantName;
            userContext.Roles = authenticated.Roles;
            userContext.Permissions = authenticated.Permissions;
            userContext.LicensedModules = authenticated.LicensedModules;
            userContext.ExpiresAt = authenticated.ExpiresAt;

            if (string.IsNullOrEmpty(userContext.UserId))
            {
                _logger.LogWarning("Request without a valid authenticated token");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "Unauthorized: Missing authentication" });
                return;
            }

            if (userContext.IsTokenExpired)
            {
                _logger.LogWarning("Token expired for user: {UserId}", userContext.UserId);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "Unauthorized: Token expired" });
                return;
            }

            if (!_openPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                && !userContext.HasModule(ModuleCodes.Accounting))
            {
                _logger.LogWarning("Tenant {TenantId} rejected: not licensed for module {ModuleCode}", userContext.TenantId, ModuleCodes.Accounting);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = $"Your organization is not licensed for this module ('{ModuleCodes.Accounting}')." });
                return;
            }

            _logger.LogInformation("User {UserId} authenticated with permissions: {Permissions}",
                userContext.UserId, string.Join(", ", userContext.Permissions));

            await _next(context);
        }
    }
}
