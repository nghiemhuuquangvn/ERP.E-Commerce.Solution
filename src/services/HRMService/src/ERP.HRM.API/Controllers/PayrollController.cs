using Asp.Versioning;
using ERP.HRM.Application.Commands;
using ERP.HRM.Application.DTOs;
using ERP.HRM.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.HRM.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
public class PayrollController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayrollController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Process payroll for all active employees for a given year/month.
    /// </summary>
    [HttpPost("api/v{version:apiVersion}/hrm/payroll/process")]
    [AuthorizePermission("hrm:payroll:manage")]
    [ProducesResponseType(typeof(List<PaySlipDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ProcessPayroll(
        [FromBody] ProcessPayrollRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new ProcessPayrollCommand(request.Year, request.Month),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Generate a single pay slip for a specific employee.
    /// </summary>
    [HttpPost("api/v{version:apiVersion}/hrm/payroll/generate")]
    [AuthorizePermission("hrm:payroll:manage")]
    [ProducesResponseType(typeof(PaySlipDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GeneratePaySlip(
        [FromBody] GeneratePaySlipCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetPaySlipById), new { id = result.Id }, result);
    }

    /// <summary>
    /// List pay slips with optional filtering by employee, year, and month.
    /// </summary>
    [HttpGet("api/v{version:apiVersion}/hrm/payslips")]
    [AuthorizePermission("hrm:payroll:manage")]
    [ProducesResponseType(typeof(PagedResult<PaySlipDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListPaySlips(
        [FromQuery] Guid? employeeId = null,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new ListPaySlipsQuery(employeeId, year, month, page, pageSize),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a pay slip by ID.
    /// </summary>
    [HttpGet("api/v{version:apiVersion}/hrm/payslips/{id:guid}")]
    [AuthorizePermission("hrm:payroll:manage")]
    [ProducesResponseType(typeof(PaySlipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPaySlipById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetPaySlipQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Pay slip {id} not found." });

        return Ok(result);
    }
}

public record ProcessPayrollRequest(int Year, int Month);
