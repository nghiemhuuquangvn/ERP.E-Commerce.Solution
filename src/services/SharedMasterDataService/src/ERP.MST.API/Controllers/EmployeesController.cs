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
[Route("api/v{version:apiVersion}/masterdata/employees")]
public class EmployeesController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmployeesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List employees with optional filtering by department and status.
    /// </summary>
    [HttpGet]
    [AuthorizePermission("masterdata:employees:view")]
    [ProducesResponseType(typeof(PagedResult<EmployeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? departmentId = null,
        [FromQuery] int? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new ListEmployeesQuery(departmentId, status, page, pageSize),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Hire a new employee.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("masterdata:employees:manage")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Hire(
        [FromBody] HireEmployeeCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get an employee by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("masterdata:employees:view")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetEmployeeQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Employee {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Terminate an employee.
    /// </summary>
    [HttpPost("{id:guid}/terminate")]
    [AuthorizePermission("masterdata:employees:manage")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Terminate(
        [FromRoute] Guid id,
        [FromBody] TerminateRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new TerminateEmployeeCommand(id, request.Reason), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Transfer an employee to a new department.
    /// </summary>
    [HttpPost("{id:guid}/transfer")]
    [AuthorizePermission("masterdata:employees:manage")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Transfer(
        [FromRoute] Guid id,
        [FromBody] TransferRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new TransferEmployeeCommand(id, request.NewDepartmentId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Update an employee's profile (email, phone).
    /// </summary>
    [HttpPut("{id:guid}/profile")]
    [AuthorizePermission("masterdata:employees:manage")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateProfile(
        [FromRoute] Guid id,
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new UpdateEmployeeProfileCommand(id, request.Email, request.Phone),
            cancellationToken);
        return Ok(result);
    }
}

public record TerminateRequest(string Reason);
public record TransferRequest(Guid NewDepartmentId);
public record UpdateProfileRequest(string? Email, string? Phone);
