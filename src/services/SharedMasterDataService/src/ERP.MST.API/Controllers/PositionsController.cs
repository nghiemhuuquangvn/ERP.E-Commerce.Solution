using ERP.MST.Application.Commands;
using ERP.MST.Application.DTOs;
using ERP.MST.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MST.API.Controllers;

[ApiController]
[Route("api/v1/masterdata/positions")]
public class PositionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PositionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List positions (paginated), optionally filtered by department.
    /// </summary>
    [HttpGet]
    [AuthorizePermission("masterdata:positions:view")]
    [ProducesResponseType(typeof(PagedResult<PositionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? departmentId = null,
        [FromQuery] string? languageCode = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListPositionsQuery(departmentId, languageCode, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new position.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("masterdata:positions:manage")]
    [ProducesResponseType(typeof(PositionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePositionCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get a position by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("masterdata:positions:view")]
    [ProducesResponseType(typeof(PositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        [FromQuery] string? languageCode = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetPositionQuery(id, languageCode), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Position {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Update an existing position.
    /// </summary>
    [HttpPut("{id:guid}")]
    [AuthorizePermission("masterdata:positions:manage")]
    [ProducesResponseType(typeof(PositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdatePositionRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdatePositionCommand(id, request.Title, request.Description);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deactivate a position.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [AuthorizePermission("masterdata:positions:manage")]
    [ProducesResponseType(typeof(PositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Deactivate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DeactivatePositionCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Activate a position.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [AuthorizePermission("masterdata:positions:manage")]
    [ProducesResponseType(typeof(PositionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Activate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ActivatePositionCommand(id), cancellationToken);
        return Ok(result);
    }
}

public record UpdatePositionRequest(string Title, string? Description);
