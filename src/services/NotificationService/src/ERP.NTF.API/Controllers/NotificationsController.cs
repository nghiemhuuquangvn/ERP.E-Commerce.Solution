using ERP.NTF.Application.Commands;
using ERP.NTF.Application.DTOs;
using ERP.NTF.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.NTF.API.Controllers;

[ApiController]
[Route("api/v1/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public NotificationsController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendNotificationCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("notifications:send"))
            return Forbid();

        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("notifications:view"))
            return Forbid();

        var result = await _mediator.Send(new GetNotificationQuery { NotificationId = id }, ct);
        if (result == null)
            return NotFound();

        // Ownership check: a notification is scoped to its RecipientId. Holding
        // notifications:view alone is not enough — without this, any caller with the
        // view permission could read ANY other recipient's notification by guessing/
        // enumerating NotificationId. Cross-recipient access requires the elevated,
        // admin-scope notifications:send permission (mirrors PreferencesController).
        var isOwnRecipient = string.Equals(result.RecipientId, _userContext.UserId, StringComparison.OrdinalIgnoreCase);
        if (!isOwnRecipient && !_userContext.HasPermission("notifications:send"))
            return Forbid();

        return Ok(result);
    }

    [HttpGet("history/{recipientId}")]
    public async Task<IActionResult> GetHistory(
        string recipientId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        if (!_userContext.HasPermission("notifications:view"))
            return Forbid();

        // Same ownership rule as GetById: only the recipient themself, or a caller
        // with the elevated notifications:send permission, may read this history.
        var isOwnRecipient = string.Equals(recipientId, _userContext.UserId, StringComparison.OrdinalIgnoreCase);
        if (!isOwnRecipient && !_userContext.HasPermission("notifications:send"))
            return Forbid();

        var result = await _mediator.Send(new GetNotificationHistoryQuery
        {
            RecipientId = recipientId,
            Skip = skip,
            Take = take
        }, ct);
        return Ok(result);
    }

    [HttpPost("bulk")]
    [ProducesResponseType(typeof(BulkSendResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkSend([FromBody] BulkSendNotificationCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("notifications:send"))
            return Forbid();

        var result = await _mediator.Send(command, ct);
        return Accepted(result);
    }
}
