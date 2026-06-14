using System.Security.Claims;
using ERP.PO.Application.Authorization;

namespace ERP.PO.Api.Authorization;

/// <summary>
/// Middleware để xác thực user từ Authorization header (JWT token hoặc API Key)
/// </summary>
public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthenticationMiddleware> _logger;

    public AuthenticationMiddleware(RequestDelegate next, ILogger<AuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();

            if (!string.IsNullOrEmpty(authHeader))
            {
                // Parse Authorization header
                if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var token = authHeader["Bearer ".Length..].Trim();
                    context.Items["JwtToken"] = token;
                    _logger.LogInformation("JWT token found in request");
                }
                else if (authHeader.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
                {
                    var apiKey = authHeader["ApiKey ".Length..].Trim();
                    context.Items["ApiKey"] = apiKey;
                    _logger.LogInformation("API key found in request");
                }
            }
            else
            {
                // Nếu không có Authorization header, tạo anonymous user
                var identity = new ClaimsIdentity(new Claim[]
                {
                    new Claim("sub", "anonymous"),
                    new Claim(ClaimTypes.Name, "Anonymous"),
                    new Claim("tenant_id", "default")
                }, "Anonymous");

                context.User = new ClaimsPrincipal(identity);
                _logger.LogInformation("No authorization header found, using anonymous user");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing authentication middleware");
        }

        await _next(context);
    }
}
