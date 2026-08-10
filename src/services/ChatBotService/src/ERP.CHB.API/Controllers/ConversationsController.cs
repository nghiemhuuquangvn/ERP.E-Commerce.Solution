using Asp.Versioning;
using ERP.CHB.Application.Commands;
using ERP.CHB.Application.Queries;
using ERP.CHB.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.CHB.API.Controllers;

/// <summary>
/// REST surface for conversation lifecycle/history — Pattern A synchronous REST, same as
/// every other service's controllers, through the Ocelot Gateway. The actual message
/// send/stream happens over ChatHub (SignalR), not here — see docs on ChatHub.SendMessage.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/chatbot/conversations")]
[Microsoft.AspNetCore.Authorization.Authorize]
public class ConversationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ConversationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>AllowAnonymous so a Storefront guest can start a conversation with no JWT at all — see StartConversationCommandHandler for how the guest path is scoped (Storefront channel only, free provider only, requires GuestTenantId/GuestSessionId).</summary>
    [HttpPost]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> Start([FromBody] StartConversationRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new StartConversationCommand(request.Channel, request.GuestTenantId, request.GuestSessionId), ct);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> ListMine([FromQuery] ConversationChannel? channel, CancellationToken ct)
    {
        var result = await _mediator.Send(new ListMyConversationsQuery(channel), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetConversationHistoryQuery(id), ct);
        return Ok(result);
    }
}

public record StartConversationRequest(ConversationChannel Channel, Guid? GuestTenantId, string? GuestSessionId);
