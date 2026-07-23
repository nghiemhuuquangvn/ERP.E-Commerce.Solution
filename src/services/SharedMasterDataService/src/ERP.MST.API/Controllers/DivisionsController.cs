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
[Route("api/v{version:apiVersion}/masterdata/divisions")]
public class DivisionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DivisionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List divisions (paginated), optionally filtered by branch.
    /// </summary>
    [HttpGet]
    [AuthorizePermission("masterdata:divisions:view")]
    [ProducesResponseType(typeof(PagedResult<DivisionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? branchId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListDivisionsQuery(branchId, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new division.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("masterdata:divisions:manage")]
    [ProducesResponseType(typeof(DivisionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateDivisionCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get a division by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("masterdata:divisions:view")]
    [ProducesResponseType(typeof(DivisionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetDivisionQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Division {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Update an existing division.
    /// </summary>
    [HttpPut("{id:guid}")]
    [AuthorizePermission("masterdata:divisions:manage")]
    [ProducesResponseType(typeof(DivisionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateDivisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateDivisionCommand(id, request.Name, request.Code, request.ParentDivisionId);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deactivate a division.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [AuthorizePermission("masterdata:divisions:manage")]
    [ProducesResponseType(typeof(DivisionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Deactivate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DeactivateDivisionCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Activate a division.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [AuthorizePermission("masterdata:divisions:manage")]
    [ProducesResponseType(typeof(DivisionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Activate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ActivateDivisionCommand(id), cancellationToken);
        return Ok(result);
    }
}

public record UpdateDivisionRequest(string Name, string Code, Guid? ParentDivisionId);
