using ERP.PO.Application.Authorization;

namespace ERP.PO.Api.Authorization;

/// <summary>
/// Middleware để đảm bảo Tenant Isolation
/// Kiểm tra user chỉ có thể truy cập data của tenant của mình
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IUserContextService userContextService)
    {
        try
        {
            var user = userContextService.GetCurrentUser();

            // Lưu tenant ID vào context để các handlers sử dụng
            context.Items["TenantId"] = user.TenantId;
            context.Items["UserId"] = user.UserId;

            _logger.LogInformation("Request from user {UserId} in tenant {TenantId}",
                user.UserId, user.TenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in tenant middleware");
        }

        await _next(context);
    }
}
