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
[Route("api/v{version:apiVersion}/masterdata/cost-centers")]
public class CostCentersController : ControllerBase
{
    private readonly IMediator _mediator;

    public CostCentersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List cost centers (paginated), optionally filtered by company.
    /// </summary>
    [HttpGet]
    [AuthorizePermission("masterdata:cost-centers:view")]
    [ProducesResponseType(typeof(PagedResult<CostCenterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListCostCentersQuery(companyId, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new cost center.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("masterdata:cost-centers:manage")]
    [ProducesResponseType(typeof(CostCenterDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCostCenterCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get a cost center by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("masterdata:cost-centers:view")]
    [ProducesResponseType(typeof(CostCenterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCostCenterQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"CostCenter {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Update an existing cost center.
    /// </summary>
    [HttpPut("{id:guid}")]
    [AuthorizePermission("masterdata:cost-centers:manage")]
    [ProducesResponseType(typeof(CostCenterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateCostCenterRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateCostCenterCommand(id, request.Name, request.Code, request.BudgetAmount, request.DepartmentId);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deactivate a cost center.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [AuthorizePermission("masterdata:cost-centers:manage")]
    [ProducesResponseType(typeof(CostCenterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Deactivate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new DeactivateCostCenterCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Activate a cost center.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [AuthorizePermission("masterdata:cost-centers:manage")]
    [ProducesResponseType(typeof(CostCenterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Activate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ActivateCostCenterCommand(id), cancellationToken);
        return Ok(result);
    }
}

public record UpdateCostCenterRequest(string Name, string Code, decimal BudgetAmount, Guid? DepartmentId);
