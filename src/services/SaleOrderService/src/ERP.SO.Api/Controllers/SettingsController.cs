using ERP.SO.Application.DTOs.Settings;
using ERP.SO.Application.Interfaces;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.SO.Api.Controllers;

/// <summary>
/// Cart/Checkout/Customer and Order settings — System (platform-wide) and Tenant
/// (per-company) tiers, mirroring IdentityService's Auth Settings pattern.
/// Storage+CRUD only for now, not yet wired into runtime behavior.
/// </summary>
[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ICartCheckoutSettingsService _cartCheckoutSettings;
    private readonly IOrderSettingsService _orderSettings;
    private readonly IUserContextService _userContextService;

    public SettingsController(
        ICartCheckoutSettingsService cartCheckoutSettings,
        IOrderSettingsService orderSettings,
        IUserContextService userContextService)
    {
        _cartCheckoutSettings = cartCheckoutSettings;
        _orderSettings = orderSettings;
        _userContextService = userContextService;
    }

    // ── Cart/Checkout/Customer ──────────────────────────────────────────

    [HttpGet("cart-checkout/system")]
    public async Task<IActionResult> GetCartCheckoutSystemSettings(CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:cart-checkout:system:view"))
            return Forbid();
        return Ok(await _cartCheckoutSettings.GetSystemSettingsAsync(ct));
    }

    [HttpPut("cart-checkout/system")]
    public async Task<IActionResult> UpdateCartCheckoutSystemSettings([FromBody] UpdateSystemCartCheckoutSettingsRequest request, CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:cart-checkout:system:manage"))
            return Forbid();
        return Ok(await _cartCheckoutSettings.UpdateSystemSettingsAsync(request, ct));
    }

    [HttpGet("cart-checkout/tenant/{tenantId}")]
    public async Task<IActionResult> GetCartCheckoutTenantSettings([FromRoute] Guid tenantId, CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:cart-checkout:tenant:view") || !CallerCanAccessTenant(tenantId, "settings:cart-checkout:system:manage"))
            return Forbid();
        return Ok(await _cartCheckoutSettings.GetTenantSettingsAsync(tenantId, ct));
    }

    [HttpPut("cart-checkout/tenant/{tenantId}")]
    public async Task<IActionResult> UpdateCartCheckoutTenantSettings([FromRoute] Guid tenantId, [FromBody] UpdateTenantCartCheckoutSettingsRequest request, CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:cart-checkout:tenant:manage") || !CallerCanAccessTenant(tenantId, "settings:cart-checkout:system:manage"))
            return Forbid();
        return Ok(await _cartCheckoutSettings.UpdateTenantSettingsAsync(tenantId, request, ct));
    }

    // ── Order ────────────────────────────────────────────────────────────

    [HttpGet("order/system")]
    public async Task<IActionResult> GetOrderSystemSettings(CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:order:system:view"))
            return Forbid();
        return Ok(await _orderSettings.GetSystemSettingsAsync(ct));
    }

    [HttpPut("order/system")]
    public async Task<IActionResult> UpdateOrderSystemSettings([FromBody] UpdateSystemOrderSettingsRequest request, CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:order:system:manage"))
            return Forbid();
        return Ok(await _orderSettings.UpdateSystemSettingsAsync(request, ct));
    }

    [HttpGet("order/tenant/{tenantId}")]
    public async Task<IActionResult> GetOrderTenantSettings([FromRoute] Guid tenantId, CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:order:tenant:view") || !CallerCanAccessTenant(tenantId, "settings:order:system:manage"))
            return Forbid();
        return Ok(await _orderSettings.GetTenantSettingsAsync(tenantId, ct));
    }

    [HttpPut("order/tenant/{tenantId}")]
    public async Task<IActionResult> UpdateOrderTenantSettings([FromRoute] Guid tenantId, [FromBody] UpdateTenantOrderSettingsRequest request, CancellationToken ct)
    {
        if (!_userContextService.HasPermission("settings:order:tenant:manage") || !CallerCanAccessTenant(tenantId, "settings:order:system:manage"))
            return Forbid();
        return Ok(await _orderSettings.UpdateTenantSettingsAsync(tenantId, request, ct));
    }

    /// <summary>Caller may only manage their own tenant's settings, unless they also hold the given system:manage override permission.</summary>
    private bool CallerCanAccessTenant(Guid tenantId, string systemManagePermission)
    {
        var caller = _userContextService.GetCurrentUser();
        if (Guid.TryParse(caller.TenantId, out var callerTenantId) && callerTenantId == tenantId)
            return true;

        return _userContextService.HasPermission(systemManagePermission);
    }
}
