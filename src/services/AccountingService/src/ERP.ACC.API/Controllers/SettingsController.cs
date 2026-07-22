using Asp.Versioning;
using ERP.ACC.Application.DTOs.Settings;
using ERP.ACC.Application.Interfaces;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.ACC.API.Controllers;

/// <summary>
/// Tax settings — System (platform-wide) and Tenant (per-company) tiers, mirroring
/// IdentityService's Auth Settings pattern. Storage+CRUD only for now.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/settings/tax")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ITaxSettingsService _settingsService;
    private readonly IUserContextService _userContextService;

    public SettingsController(ITaxSettingsService settingsService, IUserContextService userContextService)
    {
        _settingsService = settingsService;
        _userContextService = userContextService;
    }

    [HttpGet("system")]
    public async Task<IActionResult> GetSystemSettings(CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:tax:system:view"))
            return Forbid();
        return Ok(await _settingsService.GetSystemSettingsAsync(ct));
    }

    [HttpPut("system")]
    public async Task<IActionResult> UpdateSystemSettings([FromBody] UpdateSystemTaxSettingsRequest request, CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:tax:system:manage"))
            return Forbid();
        return Ok(await _settingsService.UpdateSystemSettingsAsync(request, ct));
    }

    [HttpGet("tenant/{tenantId}")]
    public async Task<IActionResult> GetTenantSettings([FromRoute] Guid tenantId, CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:tax:tenant:view") || !CallerCanAccessTenant(tenantId))
            return Forbid();
        return Ok(await _settingsService.GetTenantSettingsAsync(tenantId, ct));
    }

    [HttpPut("tenant/{tenantId}")]
    public async Task<IActionResult> UpdateTenantSettings([FromRoute] Guid tenantId, [FromBody] UpdateTenantTaxSettingsRequest request, CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:tax:tenant:manage") || !CallerCanAccessTenant(tenantId))
            return Forbid();
        return Ok(await _settingsService.UpdateTenantSettingsAsync(tenantId, request, ct));
    }

    private bool CallerCanAccessTenant(Guid tenantId)
    {
        var caller = _userContextService.GetCurrentUser();
        if (Guid.TryParse(caller.TenantId, out var callerTenantId) && callerTenantId == tenantId)
            return true;

        return _userContextService.HasPermission("settings:tax:system:manage");
    }
}
