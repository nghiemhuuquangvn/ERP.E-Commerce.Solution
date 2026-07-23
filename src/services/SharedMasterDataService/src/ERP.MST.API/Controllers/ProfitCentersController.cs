using Asp.Versioning;
using ERP.MST.Application.Commands;
using ERP.MST.Application.DTOs;
using ERP.MST.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MST.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/masterdata/profit-centers")]
public class ProfitCentersController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProfitCentersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List profit centers (paginated), optionally filtered by company.
    /// </summary>
    [HttpGet]
    [AuthorizePermission("masterdata:profit-centers:view")]
    [ProducesResponseType(typeof(PagedResult<ProfitCenterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListProfitCentersQuery(companyId, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new profit center.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("masterdata:profit-centers:manage")]
    [ProducesResponseType(typeof(ProfitCenterDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProfitCenterCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get a profit center by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("masterdata:profit-centers:view")]
    [ProducesResponseType(typeof(ProfitCenterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetProfitCenterQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"ProfitCenter {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Update an existing profit center.
    /// </summary>
    [HttpPut("{id:guid}")]
    [AuthorizePermission("masterdata:profit-centers:manage")]
    [ProducesResponseType(typeof(ProfitCenterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateProfitCenterRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateProfitCenterCommand(id, request.Name, request.Code, request.BusinessUnitId);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deactivate a profit center.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [AuthorizePermission("masterdata:profit-centers:manage")]
    [ProducesResponseType(typeof(ProfitCenterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Deactivate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DeactivateProfitCenterCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Activate a profit center.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [AuthorizePermission("masterdata:profit-centers:manage")]
    [ProducesResponseType(typeof(ProfitCenterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Activate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ActivateProfitCenterCommand(id), cancellationToken);
        return Ok(result);
    }
}

public record UpdateProfitCenterRequest(string Name, string Code, Guid? BusinessUnitId);
