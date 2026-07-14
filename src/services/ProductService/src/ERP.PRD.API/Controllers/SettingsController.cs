using ERP.PRD.Application.DTOs.Settings;
using ERP.PRD.Application.Interfaces.Services;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRD.API.Controllers;

/// <summary>
/// Store/Product settings — System (platform-wide) and Tenant (per-company) tiers,
/// mirroring IdentityService's Auth Settings pattern. ProductService has no
/// fine-grained [RequirePermission] attribute infrastructure yet (only [Authorize]),
/// so gating here uses IUserContextService.HasPermission(...) inline — the same
/// lighter-weight pattern SaleOrdersController already uses in this codebase.
/// </summary>
[ApiController]
[Route("api/settings/store-product")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly IStoreProductSettingsService _settingsService;
    private readonly IUserContextService _userContextService;

    public SettingsController(IStoreProductSettingsService settingsService, IUserContextService userContextService)
    {
        _settingsService = settingsService;
        _userContextService = userContextService;
    }

    [HttpGet("system")]
    public async Task<IActionResult> GetSystemSettings(CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:store-product:system:view"))
            return Forbid();

        return Ok(await _settingsService.GetSystemSettingsAsync(ct));
    }

    [HttpPut("system")]
    public async Task<IActionResult> UpdateSystemSettings([FromBody] UpdateSystemStoreProductSettingsRequest request, CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:store-product:system:manage"))
            return Forbid();

        return Ok(await _settingsService.UpdateSystemSettingsAsync(request, ct));
    }

    [HttpGet("tenant/{tenantId}")]
    public async Task<IActionResult> GetTenantSettings([FromRoute] Guid tenantId, CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:store-product:tenant:view") || !CallerCanAccessTenant(tenantId))
            return Forbid();

        return Ok(await _settingsService.GetTenantSettingsAsync(tenantId, ct));
    }

    [HttpPut("tenant/{tenantId}")]
    public async Task<IActionResult> UpdateTenantSettings([FromRoute] Guid tenantId, [FromBody] UpdateTenantStoreProductSettingsRequest request, CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:store-product:tenant:manage") || !CallerCanAccessTenant(tenantId))
            return Forbid();

        return Ok(await _settingsService.UpdateTenantSettingsAsync(tenantId, request, ct));
    }

    /// <summary>Caller may only manage their own tenant's settings, unless they also hold the system:manage permission (cross-tenant override).</summary>
    private bool CallerCanAccessTenant(Guid tenantId)
    {
        var caller = _userContextService.GetCurrentUser();
        if (Guid.TryParse(caller.TenantId, out var callerTenantId) && callerTenantId == tenantId)
            return true;

        return _userContextService.HasPermission("settings:store-product:system:manage");
    }
}
