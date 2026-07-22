using Asp.Versioning;
using ERP.NTF.Application.Commands;
using ERP.NTF.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.NTF.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/preferences")]
public class PreferencesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public PreferencesController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// A caller may only read/write their OWN preferences (userId must match their own
    /// identity) unless they hold an elevated, admin-scope permission (Notifications_Send)
    /// allowing cross-user access — Notifications_View alone is not enough, since these
    /// actions are keyed by a caller-suppliable userId route param (IDOR otherwise).
    /// </summary>
    private bool CanAccess(Guid userId) =>
        string.Equals(userId.ToString(), _userContext.UserId, StringComparison.OrdinalIgnoreCase)
        || _userContext.HasPermission(PermissionConstants.Notifications_Send);

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> Get(Guid userId, CancellationToken ct)
    {
        if (!_userContext.HasPermission(PermissionConstants.Notifications_View))
            return Forbid();
        if (!CanAccess(userId))
            return Forbid();

        var result = await _mediator.Send(new GetPreferenceQuery { UserId = userId }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> Update(Guid userId, [FromBody] UpdatePreferenceCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission(PermissionConstants.Notifications_View))
            return Forbid();
        if (!CanAccess(userId))
            return Forbid();

        command.UserId = userId;
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{userId:guid}/unsubscribe")]
    public async Task<IActionResult> Unsubscribe(Guid userId, CancellationToken ct)
    {
        if (!_userContext.HasPermission(PermissionConstants.Notifications_View))
            return Forbid();
        if (!CanAccess(userId))
            return Forbid();

        await _mediator.Send(new UnsubscribeCommand { UserId = userId }, ct);
        return NoContent();
    }
}
