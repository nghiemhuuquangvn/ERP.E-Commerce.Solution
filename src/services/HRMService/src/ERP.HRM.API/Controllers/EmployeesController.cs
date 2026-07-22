using Asp.Versioning;
using ERP.HRM.Application.DTOs;
using ERP.HRM.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.HRM.API.Controllers;

/// <summary>
/// Read-only view of HRM's local EmployeeSnapshot projection.
/// Employee lifecycle (hire/terminate/transfer) is managed by SharedMasterDataService;
/// this controller exposes the denormalised HRM view for attendance/payroll consumers.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hrm/employees")]
public class EmployeesController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmployeesController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// List employees visible to HRM (from local EmployeeSnapshot projection) with
    /// optional search, department and employment-status filters.
    /// </summary>
    [HttpGet]
    [AuthorizePermission("hrm:employees:view")]
    [ProducesResponseType(typeof(PagedResult<EmployeeSnapshotDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] Guid? departmentId,
        [FromQuery] int? employmentStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new ListEmployeeSnapshotsQuery(search, departmentId, employmentStatus, page, pageSize),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a single employee snapshot by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("hrm:employees:view")]
    [ProducesResponseType(typeof(EmployeeSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetEmployeeSnapshotQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Employee {id} not found in HRM snapshot." });
        return Ok(result);
    }
}
