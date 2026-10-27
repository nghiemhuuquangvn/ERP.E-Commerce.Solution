using Asp.Versioning;
using ERP.PAY.Application.DTOs.Settings;
using ERP.PAY.Application.Interfaces;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PAY.API.Controllers;

/// <summary>
/// Payment settings — System (platform-wide gateway availability) and Tenant
/// (per-company gateway enablement + credentials) tiers, mirroring IdentityService's
/// Auth Settings pattern. Storage+CRUD only for now.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/settings/payment")]
public class SettingsController : ControllerBase
{
    private readonly IPaymentSettingsService _settingsService;
    private readonly UserContext _userContext;

    public SettingsController(IPaymentSettingsService settingsService, UserContext userContext)
    {
        _settingsService = settingsService;
        _userContext = userContext;
    }

    [HttpGet("system")]
    public async Task<IActionResult> GetSystemSettings(CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:payment:system:view"))
            return Forbid();
        return Ok(await _settingsService.GetSystemSettingsAsync(ct));
    }

    [HttpPut("system")]
    public async Task<IActionResult> UpdateSystemSettings([FromBody] UpdateSystemPaymentSettingsRequest request, CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:payment:system:manage"))
            return Forbid();
        return Ok(await _settingsService.UpdateSystemSettingsAsync(request, ct));
    }

    [HttpGet("tenant/{tenantId}")]
    public async Task<IActionResult> GetTenantSettings([FromRoute] Guid tenantId, CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:payment:tenant:view") || !CallerCanAccessTenant(tenantId))
            return Forbid();
        return Ok(await _settingsService.GetTenantSettingsAsync(tenantId, ct));
    }

    [HttpPut("tenant/{tenantId}")]
    public async Task<IActionResult> UpdateTenantSettings([FromRoute] Guid tenantId, [FromBody] UpdateTenantPaymentSettingsRequest request, CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:payment:tenant:manage") || !CallerCanAccessTenant(tenantId))
            return Forbid();
        return Ok(await _settingsService.UpdateTenantSettingsAsync(tenantId, request, ct));
    }

    /// <summary>The tenant's merchant accounts at VNPay, MoMo and ZaloPay. Secrets are never returned, only whether they are set.</summary>
    [HttpGet("tenant/{tenantId}/gateways")]
    public async Task<IActionResult> GetGateways([FromRoute] Guid tenantId, CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:payment:tenant:view") || !CallerCanAccessTenant(tenantId))
            return Forbid();
        return Ok(await _settingsService.GetGatewaysAsync(tenantId, ct));
    }

    [HttpPut("tenant/{tenantId}/gateways/{provider}")]
    public async Task<IActionResult> UpdateGateway([FromRoute] Guid tenantId, [FromRoute] string provider, [FromBody] UpdateGatewayCredentialRequest request, CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:payment:tenant:manage") || !CallerCanAccessTenant(tenantId))
            return Forbid();
        try
        {
            return Ok(await _settingsService.UpdateGatewayAsync(tenantId, provider, request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private bool CallerCanAccessTenant(Guid tenantId)
    {
        if (Guid.TryParse(_userContext.TenantId, out var callerTenantId) && callerTenantId == tenantId)
            return true;

        return _userContext.HasPermission("settings:payment:system:manage");
    }
}
