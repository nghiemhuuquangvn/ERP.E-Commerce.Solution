using ERP.MST.Application.DTOs.Settings;
using ERP.MST.Application.Interfaces;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MST.API.Controllers;

/// <summary>
/// Currency settings — System (platform-wide) and Tenant (per-company) tiers,
/// mirroring IdentityService's Auth Settings pattern. Storage+CRUD only for now.
/// </summary>
[ApiController]
[Route("api/settings/currency")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ICurrencySettingsService _settingsService;
    private readonly IUserContextService _userContextService;

    public SettingsController(ICurrencySettingsService settingsService, IUserContextService userContextService)
    {
        _settingsService = settingsService;
        _userContextService = userContextService;
    }

    [HttpGet("system")]
    public async Task<IActionResult> GetSystemSettings(CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:currency:system:view"))
            return Forbid();
        return Ok(await _settingsService.GetSystemSettingsAsync(ct));
    }

    [HttpPut("system")]
    public async Task<IActionResult> UpdateSystemSettings([FromBody] UpdateSystemCurrencySettingsRequest request, CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:currency:system:manage"))
            return Forbid();
        return Ok(await _settingsService.UpdateSystemSettingsAsync(request, ct));
    }

    [HttpGet("tenant/{tenantId}")]
    public async Task<IActionResult> GetTenantSettings([FromRoute] Guid tenantId, CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:currency:tenant:view") || !CallerCanAccessTenant(tenantId))
            return Forbid();
        return Ok(await _settingsService.GetTenantSettingsAsync(tenantId, ct));
    }

    [HttpPut("tenant/{tenantId}")]
    public async Task<IActionResult> UpdateTenantSettings([FromRoute] Guid tenantId, [FromBody] UpdateTenantCurrencySettingsRequest request, CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:currency:tenant:manage") || !CallerCanAccessTenant(tenantId))
            return Forbid();
        return Ok(await _settingsService.UpdateTenantSettingsAsync(tenantId, request, ct));
    }

    private bool CallerCanAccessTenant(Guid tenantId)
    {
        var caller = _userContextService.GetCurrentUser();
        if (Guid.TryParse(caller.TenantId, out var callerTenantId) && callerTenantId == tenantId)
            return true;

        return _userContextService.HasPermission("settings:currency:system:manage");
    }
}
