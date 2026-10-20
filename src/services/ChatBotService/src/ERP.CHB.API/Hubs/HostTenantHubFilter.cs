using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;

namespace ERP.CHB.API.Hubs;

/// <summary>
/// /hubs/chat is proxied by the SPA's nginx straight to this service — it never passes the
/// Gateway, so nothing has resolved the tenant from the host (and nginx drops any
/// client-sent X-Tenant-* header). This filter does it for every hub call:
/// <list type="bullet">
/// <item>the request host is resolved to a tenant through IdentityService (cached);
/// unknown host = the call is refused;</item>
/// <item>a signed-in caller's token must belong to that same tenant (the check the
/// Gateway does for every API call);</item>
/// <item>the invocation then runs inside <see cref="TenantScope"/>, so ChatBotDbContext
/// only ever sees that tenant's conversations — a guest of tenant A can't join or post
/// into tenant B's conversations.</item>
/// </list>
/// </summary>
public sealed class HostTenantHubFilter : IHubFilter
{
    private readonly TenantHostResolver _resolver;
    private readonly ILogger<HostTenantHubFilter> _logger;

    public HostTenantHubFilter(TenantHostResolver resolver, ILogger<HostTenantHubFilter> logger)
    {
        _resolver = resolver;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var context = invocationContext.Context;
        var host = context.GetHttpContext()?.Request.Host.Host;
        var tenant = string.IsNullOrEmpty(host) ? null : await _resolver.ResolveAsync(host, context.ConnectionAborted);
        if (tenant is null)
        {
            _logger.LogWarning("Hub call {Method} refused: unknown tenant host {Host}", invocationContext.HubMethodName, host);
            throw new HubException("Unknown tenant host.");
        }

        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var tokenTenant = context.User.FindFirst("tenant_id")?.Value;
            if (!Guid.TryParse(tokenTenant, out var tokenTenantId) || tokenTenantId != tenant.TenantId)
            {
                _logger.LogWarning("Hub call refused: token tenant {TokenTenant} on host {Host} (tenant {HostTenant})",
                    tokenTenant, host, tenant.TenantId);
                throw new HubException("Token does not belong to this tenant.");
            }
        }

        using (TenantScope.Begin(tenant.TenantId))
            return await next(invocationContext);
    }
}
