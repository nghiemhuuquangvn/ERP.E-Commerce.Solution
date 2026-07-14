using ERP.SHP.Application.DTOs.Settings;
using ERP.SHP.Application.Interfaces;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.SHP.API.Controllers;

/// <summary>
/// Shipping settings — System (platform-wide) and Tenant (per-company) tiers,
/// mirroring IdentityService's Auth Settings pattern. Storage+CRUD only for now.
/// </summary>
[ApiController]
[Route("api/settings/shipping")]
public class SettingsController : ControllerBase
{
    private readonly IShippingSettingsService _settingsService;
    private readonly UserContext _userContext;

    public SettingsController(IShippingSettingsService settingsService, UserContext userContext)
    {
        _settingsService = settingsService;
        _userContext = userContext;
    }

    [HttpGet("system")]
    public async Task<IActionResult> GetSystemSettings(CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:shipping:system:view"))
            return Forbid();
        return Ok(await _settingsService.GetSystemSettingsAsync(ct));
    }

    [HttpPut("system")]
    public async Task<IActionResult> UpdateSystemSettings([FromBody] UpdateSystemShippingSettingsRequest request, CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:shipping:system:manage"))
            return Forbid();
        return Ok(await _settingsService.UpdateSystemSettingsAsync(request, ct));
    }

    [HttpGet("tenant/{tenantId}")]
    public async Task<IActionResult> GetTenantSettings([FromRoute] Guid tenantId, CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:shipping:tenant:view") || !CallerCanAccessTenant(tenantId))
            return Forbid();
        return Ok(await _settingsService.GetTenantSettingsAsync(tenantId, ct));
    }

    [HttpPut("tenant/{tenantId}")]
    public async Task<IActionResult> UpdateTenantSettings([FromRoute] Guid tenantId, [FromBody] UpdateTenantShippingSettingsRequest request, CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:shipping:tenant:manage") || !CallerCanAccessTenant(tenantId))
            return Forbid();
        return Ok(await _settingsService.UpdateTenantSettingsAsync(tenantId, request, ct));
    }

    private bool CallerCanAccessTenant(Guid tenantId)
    {
        if (Guid.TryParse(_userContext.TenantId, out var callerTenantId) && callerTenantId == tenantId)
            return true;

        return _userContext.HasPermission("settings:shipping:system:manage");
    }
}
