using Asp.Versioning;
using ERP.NTF.Application.Commands;
using ERP.NTF.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.NTF.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/templates")]
public class TemplatesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _userContext;

    public TemplatesController(IMediator mediator, UserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? isActive, CancellationToken ct)
    {
        var result = await _mediator.Send(new ListTemplatesQuery { IsActive = isActive }, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetTemplateQuery { TemplateId = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTemplateCommand command, CancellationToken ct)
    {
        if (!_userContext.HasPermission("notifications:templates:manage"))
            return Forbid();

        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
