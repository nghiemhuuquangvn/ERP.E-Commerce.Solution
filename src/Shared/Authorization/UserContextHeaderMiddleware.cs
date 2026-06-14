using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ERP.Shared.Authorization;

/// <summary>
/// Middleware để lưu UserContext từ headers vào HttpContext.Items
/// Cho phép services access user context qua context.Items["UserContext"]
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

    public async Task InvokeAsync(HttpContext context, IUserContextService userContextService)
    {
        try
        {
            var user = userContextService.GetCurrentUser();

            // Lưu UserContext vào context.Items
            context.Items["UserContext"] = user;
            context.Items["TenantId"] = user.TenantId;
            context.Items["UserId"] = user.UserId;

            _logger.LogInformation(
                "UserContext set: UserId={UserId}, TenantId={TenantId}",
                user.UserId, user.TenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting user context");
        }

        await _next(context);
    }
}
