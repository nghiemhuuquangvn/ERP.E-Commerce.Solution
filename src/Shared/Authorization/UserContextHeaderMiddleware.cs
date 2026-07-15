using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ERP.Shared.Authorization;

/// <summary>
/// Populates the request-scoped <see cref="UserContext"/> from validated JWT claims,
/// and also mirrors it into context.Items for callers that read it that way.
///
/// FIXED (this pass): previously this only wrote to context.Items["UserContext"], not
/// the request-scoped `UserContext` instance itself — but every controller in this
/// repo that does `_userContext.HasPermission(...)` gets that instance via constructor
/// injection (`AddScoped&lt;UserContext&gt;()`), which the DI container creates as a
/// separate, blank object with empty Permissions. That meant every
/// `_userContext.HasPermission(...)` check in every service using this middleware
/// (10 of them: HRM, Logging, Notification, Payment, POS, Promotion, PurchaseOrder,
/// Reporting, SharedMasterData, Shipping) silently, always evaluated to false —
/// permission checks fail-closed for every caller, not just unauthorized ones.
/// AccountingService/SaleOrderService each already had their own local, correct
/// version of this middleware (constructor/method-injecting the same `UserContext`
/// instance and mutating its properties in place) — this brings the shared one in
/// line with that already-proven pattern instead of leaving 10 services on a
/// silently broken permission-check path.
///
/// Deliberately does NOT hard-reject unauthenticated requests here (unlike
/// AccountingService's local version) — anonymous/`[AllowAnonymous]` endpoints across
/// the 10 services this middleware serves are not individually audited for that
/// stricter behavior, and downstream `[Authorize]`/`HasPermission` checks already
/// handle rejection correctly once `UserContext` is actually populated.
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

    public async Task InvokeAsync(HttpContext context, UserContext userContext, IUserContextService userContextService)
    {
        try
        {
            var user = userContextService.GetCurrentUser();

            // Mutate the SAME request-scoped instance controllers will receive via
            // constructor injection — not a new/different object — so
            // `_userContext.HasPermission(...)` in a controller actually sees this data.
            userContext.UserId = user.UserId;
            userContext.UserName = user.UserName;
            userContext.TenantId = user.TenantId;
            userContext.TenantName = user.TenantName;
            userContext.Roles = user.Roles;
            userContext.Permissions = user.Permissions;
            userContext.ExpiresAt = user.ExpiresAt;

            // Kept for any existing caller that reads context.Items directly.
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
