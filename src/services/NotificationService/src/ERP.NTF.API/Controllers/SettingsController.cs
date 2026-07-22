using Asp.Versioning;
using ERP.NTF.Application.DTOs.Settings;
using ERP.NTF.Application.Interfaces;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.NTF.API.Controllers;

/// <summary>
/// Notification/Email/SMS settings — System (platform-wide) and Tenant (per-company)
/// tiers, mirroring PaymentService's Settings pattern from Phase 1. Storage+CRUD only
/// for now. NotificationService has no [RequirePermission] attribute infrastructure,
/// so gating is inline via UserContext.HasPermission(...) (this service's own
/// established pattern, e.g. WebhooksController).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/settings")]
public class SettingsController : ControllerBase
{
    private readonly INotificationSettingsService _notificationSettings;
    private readonly IEmailSettingsService _emailSettings;
    private readonly ISmsSettingsService _smsSettings;
    private readonly UserContext _userContext;

    public SettingsController(
        INotificationSettingsService notificationSettings,
        IEmailSettingsService emailSettings,
        ISmsSettingsService smsSettings,
        UserContext userContext)
    {
        _notificationSettings = notificationSettings;
        _emailSettings = emailSettings;
        _smsSettings = smsSettings;
        _userContext = userContext;
    }

    // ── Notification ─────────────────────────────────────────────────────

    [HttpGet("notification/system")]
    public async Task<IActionResult> GetNotificationSystemSettings(CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:notification:system:view"))
            return Forbid();
        return Ok(await _notificationSettings.GetSystemSettingsAsync(ct));
    }

    [HttpPut("notification/system")]
    public async Task<IActionResult> UpdateNotificationSystemSettings([FromBody] UpdateSystemNotificationSettingsRequest request, CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:notification:system:manage"))
            return Forbid();
        return Ok(await _notificationSettings.UpdateSystemSettingsAsync(request, ct));
    }

    [HttpGet("notification/tenant/{tenantId}")]
    public async Task<IActionResult> GetNotificationTenantSettings([FromRoute] Guid tenantId, CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:notification:tenant:view") || !CallerCanAccessTenant(tenantId, "settings:notification:system:manage"))
            return Forbid();
        return Ok(await _notificationSettings.GetTenantSettingsAsync(tenantId, ct));
    }

    [HttpPut("notification/tenant/{tenantId}")]
    public async Task<IActionResult> UpdateNotificationTenantSettings([FromRoute] Guid tenantId, [FromBody] UpdateTenantNotificationSettingsRequest request, CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:notification:tenant:manage") || !CallerCanAccessTenant(tenantId, "settings:notification:system:manage"))
            return Forbid();
        return Ok(await _notificationSettings.UpdateTenantSettingsAsync(tenantId, request, ct));
    }

    // ── Email ─────────────────────────────────────────────────────────────

    [HttpGet("email/system")]
    public async Task<IActionResult> GetEmailSystemSettings(CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:email:system:view"))
            return Forbid();
        return Ok(await _emailSettings.GetSystemSettingsAsync(ct));
    }

    [HttpPut("email/system")]
    public async Task<IActionResult> UpdateEmailSystemSettings([FromBody] UpdateSystemEmailSettingsRequest request, CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:email:system:manage"))
            return Forbid();
        return Ok(await _emailSettings.UpdateSystemSettingsAsync(request, ct));
    }

    [HttpGet("email/tenant/{tenantId}")]
    public async Task<IActionResult> GetEmailTenantSettings([FromRoute] Guid tenantId, CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:email:tenant:view") || !CallerCanAccessTenant(tenantId, "settings:email:system:manage"))
            return Forbid();
        return Ok(await _emailSettings.GetTenantSettingsAsync(tenantId, ct));
    }

    [HttpPut("email/tenant/{tenantId}")]
    public async Task<IActionResult> UpdateEmailTenantSettings([FromRoute] Guid tenantId, [FromBody] UpdateTenantEmailSettingsRequest request, CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:email:tenant:manage") || !CallerCanAccessTenant(tenantId, "settings:email:system:manage"))
            return Forbid();
        return Ok(await _emailSettings.UpdateTenantSettingsAsync(tenantId, request, ct));
    }

    // ── SMS ───────────────────────────────────────────────────────────────

    [HttpGet("sms/system")]
    public async Task<IActionResult> GetSmsSystemSettings(CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:sms:system:view"))
            return Forbid();
        return Ok(await _smsSettings.GetSystemSettingsAsync(ct));
    }

    [HttpPut("sms/system")]
    public async Task<IActionResult> UpdateSmsSystemSettings([FromBody] UpdateSystemSmsSettingsRequest request, CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:sms:system:manage"))
            return Forbid();
        return Ok(await _smsSettings.UpdateSystemSettingsAsync(request, ct));
    }

    [HttpGet("sms/tenant/{tenantId}")]
    public async Task<IActionResult> GetSmsTenantSettings([FromRoute] Guid tenantId, CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:sms:tenant:view") || !CallerCanAccessTenant(tenantId, "settings:sms:system:manage"))
            return Forbid();
        return Ok(await _smsSettings.GetTenantSettingsAsync(tenantId, ct));
    }

    [HttpPut("sms/tenant/{tenantId}")]
    public async Task<IActionResult> UpdateSmsTenantSettings([FromRoute] Guid tenantId, [FromBody] UpdateTenantSmsSettingsRequest request, CancellationToken ct)
    {
        if (!_userContext.HasPermission("settings:sms:tenant:manage") || !CallerCanAccessTenant(tenantId, "settings:sms:system:manage"))
            return Forbid();
        return Ok(await _smsSettings.UpdateTenantSettingsAsync(tenantId, request, ct));
    }

    /// <summary>Caller may only manage their own tenant's settings, unless they also hold the given system:manage override permission.</summary>
    private bool CallerCanAccessTenant(Guid tenantId, string systemManagePermission)
    {
        if (Guid.TryParse(_userContext.TenantId, out var callerTenantId) && callerTenantId == tenantId)
            return true;

        return _userContext.HasPermission(systemManagePermission);
    }
}
