using Asp.Versioning;
using ERP.NTF.Application.Commands;
using ERP.NTF.Application.DTOs;
using ERP.NTF.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.NTF.API.Controllers;

/// <summary>
/// Message templates. A shop reads the platform defaults and its own; it changes them by overriding a default (a copy it owns) and editing the copy;
/// deleting the copy brings the default back (ROADMAP P2-1b). Reading needs <c>notifications:view</c>, every change <c>notifications:templates:manage</c>.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/templates")]
public class TemplatesController : ControllerBase
{
    private readonly IMediator _mediator;

    public TemplatesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [AuthorizePermission(PermissionConstants.Notifications_View)]
    public async Task<IActionResult> List([FromQuery] bool? isActive, CancellationToken ct) =>
        Ok(await _mediator.Send(new ListTemplatesQuery { IsActive = isActive }, ct));

    [HttpGet("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Notifications_View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTemplateQuery { TemplateId = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [AuthorizePermission(PermissionConstants.Notifications_TemplatesManage)]
    public async Task<IActionResult> Create([FromBody] CreateTemplateCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Notifications_TemplatesManage)]
    [ProducesResponseType(typeof(NotificationTemplateDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTemplateRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(new UpdateTemplateCommand { TemplateId = id, Name = request.Name, Subject = request.Subject, Body = request.Body, Variables = request.Variables }, ct));

    [HttpPost("{id:guid}/activate")]
    [AuthorizePermission(PermissionConstants.Notifications_TemplatesManage)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new SetTemplateActiveCommand { TemplateId = id, IsActive = true }, ct));

    [HttpPost("{id:guid}/deactivate")]
    [AuthorizePermission(PermissionConstants.Notifications_TemplatesManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new SetTemplateActiveCommand { TemplateId = id, IsActive = false }, ct));

    /// <summary>A shop copies a platform default into a template of its own.</summary>
    [HttpPost("{id:guid}/override")]
    [AuthorizePermission(PermissionConstants.Notifications_TemplatesManage)]
    public async Task<IActionResult> Override(Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new OverrideTemplateCommand { TemplateId = id }, ct));

    [HttpDelete("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Notifications_TemplatesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteTemplateCommand { TemplateId = id }, ct);
        return NoContent();
    }

    /// <summary>What a text becomes with sample values, and the placeholders nothing covers.</summary>
    [HttpPost("preview")]
    [AuthorizePermission(PermissionConstants.Notifications_TemplatesManage)]
    [ProducesResponseType(typeof(TemplatePreviewResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview([FromBody] PreviewTemplateCommand command, CancellationToken ct) =>
        Ok(await _mediator.Send(command, ct));

    public record UpdateTemplateRequest(string Name, string Subject, string Body, string? Variables = null);
}
