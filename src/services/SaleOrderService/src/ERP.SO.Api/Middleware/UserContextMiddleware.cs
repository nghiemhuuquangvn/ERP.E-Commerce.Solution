using ERP.Shared.Authorization;

namespace ERP.SO.Api.Middleware
{
    public static class UserContextMiddlewareExtensions
    {
        public static IApplicationBuilder UseUserContextHeaderMiddleware(this IApplicationBuilder app)
        {
            return app.UseMiddleware<UserContextHeaderMiddleware>();
        }
    }

    public class UserContextHeaderMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<UserContextHeaderMiddleware> _logger;

        public UserContextHeaderMiddleware(RequestDelegate next, ILogger<UserContextHeaderMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, UserContext userContext)
        {
            // Extract user information from headers (from API Gateway/Identity Service)
            if (context.Request.Headers.TryGetValue("X-User-Id", out var userId))
            {
                userContext.UserId = userId.ToString();
            }

            if (context.Request.Headers.TryGetValue("X-User-Name", out var userName))
            {
                userContext.UserName = userName.ToString();
            }

            if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantId))
            {
                userContext.TenantId = tenantId.ToString();
            }

            if (context.Request.Headers.TryGetValue("X-Tenant-Name", out var tenantName))
            {
                userContext.TenantName = tenantName.ToString();
            }

            // Parse permissions from comma-separated header
            if (context.Request.Headers.TryGetValue("X-Permissions", out var permissions))
            {
                var permissionArray = permissions.ToString()
                    .Split(',')
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToArray();
                userContext.Permissions = permissionArray;
            }

            // Parse roles from comma-separated header
            if (context.Request.Headers.TryGetValue("X-Roles", out var roles))
            {
                var roleArray = roles.ToString()
                    .Split(',')
                    .Select(r => r.Trim())
                    .Where(r => !string.IsNullOrEmpty(r))
                    .ToArray();
                userContext.Roles = roleArray;
            }

            // Parse token expiration
            if (context.Request.Headers.TryGetValue("X-Token-Expires", out var expiresAt))
            {
                if (long.TryParse(expiresAt.ToString(), out var timestamp))
                {
                    userContext.ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
                }
            }

            // Check authentication
            if (string.IsNullOrEmpty(userContext.UserId))
            {
                _logger.LogWarning("Request without authentication headers");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "Unauthorized: Missing authentication" });
                return;
            }

            // Check token expiration
            if (userContext.IsTokenExpired)
            {
                _logger.LogWarning("Token expired for user: {UserId}", userContext.UserId);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "Unauthorized: Token expired" });
                return;
            }

            _logger.LogInformation("User {UserId} authenticated with permissions: {Permissions}",
                userContext.UserId, string.Join(", ", userContext.Permissions));

            await _next(context);
        }
    }
}
