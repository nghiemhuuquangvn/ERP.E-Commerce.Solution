using Asp.Versioning;
using ERP.NTF.Application.Commands;
using ERP.NTF.Application.DTOs;
using ERP.NTF.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.NTF.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public WebhooksController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<WebhookDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!_userContext.HasPermission("notifications:webhooks:manage"))
            return Forbid();
        var result = await _mediator.Send(new ListWebhooksQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WebhookDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("notifications:webhooks:manage"))
            return Forbid();
        var result = await _mediator.Send(new GetWebhookQuery { WebhookId = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(WebhookDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterWebhookCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("notifications:webhooks:manage"))
            return Forbid();
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(WebhookDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWebhookRequest request, CancellationToken ct)
    {
        if (!_userContext.HasPermission("notifications:webhooks:manage"))
            return Forbid();
        var result = await _mediator.Send(new UpdateWebhookCommand
        {
            WebhookId = id,
            Name = request.Name,
            Url = request.Url,
            Secret = request.Secret,
            EventTypes = request.EventTypes ?? new()
        }, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("notifications:webhooks:manage"))
            return Forbid();
        await _mediator.Send(new DeleteWebhookCommand { WebhookId = id }, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(WebhookDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("notifications:webhooks:manage"))
            return Forbid();
        var result = await _mediator.Send(new SetWebhookActiveCommand { WebhookId = id, IsActive = false }, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(WebhookDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        if (!_userContext.HasPermission("notifications:webhooks:manage"))
            return Forbid();
        var result = await _mediator.Send(new SetWebhookActiveCommand { WebhookId = id, IsActive = true }, ct);
        return Ok(result);
    }

    public record UpdateWebhookRequest(string Name, string Url, string? Secret = null, List<string>? EventTypes = null);
}
