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
[Route("api/v{version:apiVersion}/masterdata/units-of-measure")]
public class UnitsOfMeasureController : ControllerBase
{
    private readonly IMediator _mediator;

    public UnitsOfMeasureController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List units of measure (paginated).
    /// </summary>
    [HttpGet]
    [AuthorizePermission("masterdata:units-of-measure:view")]
    [ProducesResponseType(typeof(PagedResult<UnitOfMeasureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] string? languageCode = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListUnitsOfMeasureQuery(languageCode, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new unit of measure.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("masterdata:units-of-measure:manage")]
    [ProducesResponseType(typeof(UnitOfMeasureDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateUnitOfMeasureCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get a unit of measure by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("masterdata:units-of-measure:view")]
    [ProducesResponseType(typeof(UnitOfMeasureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        [FromQuery] string? languageCode = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetUnitOfMeasureQuery(id, languageCode), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Unit of measure {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Update an existing unit of measure.
    /// </summary>
    [HttpPut("{id:guid}")]
    [AuthorizePermission("masterdata:units-of-measure:manage")]
    [ProducesResponseType(typeof(UnitOfMeasureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateUnitOfMeasureRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateUnitOfMeasureCommand(id, request.Name, request.Symbol, request.ConversionFactor);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}

public record UpdateUnitOfMeasureRequest(string Name, string Symbol, decimal ConversionFactor);
