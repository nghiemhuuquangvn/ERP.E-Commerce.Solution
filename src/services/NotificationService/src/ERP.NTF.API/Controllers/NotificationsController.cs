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
        var result = await _mediator.Send(new GetNotificationQuery { NotificationId = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("history/{recipientId}")]
    public async Task<IActionResult> GetHistory(
        string recipientId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
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
