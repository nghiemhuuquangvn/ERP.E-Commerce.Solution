using Asp.Versioning;
using ERP.NTF.Application.Commands;
using ERP.NTF.Application.DTOs;
using ERP.NTF.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.NTF.API.Controllers;

/// <summary>Webhook endpoints of the shop and their delivery log. Every action needs <c>notifications:webhooks:manage</c> (declared, not checked in the action).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/webhooks")]
[AuthorizePermission("notifications:webhooks:manage")]
public class WebhooksController : ControllerBase
{
    private readonly IMediator _mediator;

    public WebhooksController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(List<WebhookDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _mediator.Send(new ListWebhooksQuery(), ct));

    /// <summary>The events a webhook can be subscribed to (an empty subscription means all of them).</summary>
    [HttpGet("event-types")]
    [ProducesResponseType(typeof(List<WebhookEventTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> EventTypes(CancellationToken ct) => Ok(await _mediator.Send(new GetWebhookEventTypesQuery(), ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WebhookDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetWebhookQuery { WebhookId = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(WebhookDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterWebhookCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(WebhookDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWebhookRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(new UpdateWebhookCommand
        {
            WebhookId = id,
            Name = request.Name,
            Url = request.Url,
            Secret = request.Secret,
            ClearSecret = request.ClearSecret,
            EventTypes = request.EventTypes ?? new()
        }, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteWebhookCommand { WebhookId = id }, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(WebhookDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new SetWebhookActiveCommand { WebhookId = id, IsActive = false }, ct));

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(WebhookDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new SetWebhookActiveCommand { WebhookId = id, IsActive = true }, ct));

    /// <summary>Sends a sample event to the endpoint and reports what came back (also written to the delivery log).</summary>
    [HttpPost("{id:guid}/test")]
    [ProducesResponseType(typeof(WebhookTestResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Test(Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new TestWebhookCommand { WebhookId = id }, ct));

    [HttpGet("{id:guid}/deliveries")]
    [ProducesResponseType(typeof(WebhookDeliveryPage), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deliveries(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _mediator.Send(new ListWebhookDeliveriesQuery { WebhookId = id, Page = page, PageSize = pageSize }, ct));

    public record UpdateWebhookRequest(string Name, string Url, string? Secret = null, bool ClearSecret = false, List<string>? EventTypes = null);
}
