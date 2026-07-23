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
[Route("api/v{version:apiVersion}/masterdata/business-units")]
public class BusinessUnitsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BusinessUnitsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List business units (paginated), optionally filtered by company.
    /// </summary>
    [HttpGet]
    [AuthorizePermission("masterdata:business-units:view")]
    [ProducesResponseType(typeof(PagedResult<BusinessUnitDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListBusinessUnitsQuery(companyId, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new business unit.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("masterdata:business-units:manage")]
    [ProducesResponseType(typeof(BusinessUnitDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBusinessUnitCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get a business unit by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("masterdata:business-units:view")]
    [ProducesResponseType(typeof(BusinessUnitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetBusinessUnitQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"BusinessUnit {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Update an existing business unit.
    /// </summary>
    [HttpPut("{id:guid}")]
    [AuthorizePermission("masterdata:business-units:manage")]
    [ProducesResponseType(typeof(BusinessUnitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateBusinessUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateBusinessUnitCommand(id, request.Name, request.Code);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deactivate a business unit.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [AuthorizePermission("masterdata:business-units:manage")]
    [ProducesResponseType(typeof(BusinessUnitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Deactivate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DeactivateBusinessUnitCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Activate a business unit.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [AuthorizePermission("masterdata:business-units:manage")]
    [ProducesResponseType(typeof(BusinessUnitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Activate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ActivateBusinessUnitCommand(id), cancellationToken);
        return Ok(result);
    }
}

public record UpdateBusinessUnitRequest(string Name, string Code);
