using Asp.Versioning;
using ERP.HRM.Application.Commands;
using ERP.HRM.Application.DTOs;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.HRM.API.Controllers;

/// <summary>Salary terms, dependents, payroll rate sets (ROADMAP P1-7). All of it is payroll data: hrm:payroll:manage.</summary>
[ApiController]
[ApiVersion("1.0")]
[AuthorizePermission("hrm:payroll:manage")]
public class PayrollSettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayrollSettingsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Active employees with the salary terms in force today.</summary>
    [HttpGet("api/v{version:apiVersion}/hrm/salary-profiles")]
    [ProducesResponseType(typeof(PagedResult<SalaryProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSalaryProfiles(
        [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _mediator.Send(new ListSalaryProfilesQuery(search, page, pageSize), ct));

    [HttpGet("api/v{version:apiVersion}/hrm/employees/{employeeId:guid}/compensations")]
    [ProducesResponseType(typeof(List<CompensationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCompensations([FromRoute] Guid employeeId, CancellationToken ct = default) =>
        Ok(await _mediator.Send(new ListCompensationsQuery(employeeId), ct));

    /// <summary>Set the salary terms of an employee from a month on (the same month again replaces that row).</summary>
    [HttpPut("api/v{version:apiVersion}/hrm/employees/{employeeId:guid}/compensations")]
    [ProducesResponseType(typeof(CompensationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveCompensation(
        [FromRoute] Guid employeeId, [FromBody] SaveCompensationRequest body, CancellationToken ct = default) =>
        Ok(await _mediator.Send(new SaveCompensationCommand(
            employeeId, body.EffectiveFrom, body.BaseSalary, body.TaxableAllowance, body.ExemptAllowance,
            body.InsuranceSalary, body.InsuranceEnabled, body.TaxCode, body.InsuranceNumber), ct));

    [HttpGet("api/v{version:apiVersion}/hrm/employees/{employeeId:guid}/dependents")]
    [ProducesResponseType(typeof(List<DependentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDependents([FromRoute] Guid employeeId, CancellationToken ct = default) =>
        Ok(await _mediator.Send(new ListDependentsQuery(employeeId), ct));

    [HttpPost("api/v{version:apiVersion}/hrm/employees/{employeeId:guid}/dependents")]
    [ProducesResponseType(typeof(DependentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddDependent(
        [FromRoute] Guid employeeId, [FromBody] AddDependentRequest body, CancellationToken ct = default) =>
        Ok(await _mediator.Send(new AddDependentCommand(employeeId, body.FullName, body.Relationship, body.TaxCode, body.FromMonth, body.ToMonth), ct));

    [HttpDelete("api/v{version:apiVersion}/hrm/dependents/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveDependent([FromRoute] Guid id, CancellationToken ct = default)
    {
        await _mediator.Send(new RemoveDependentCommand(id), ct);
        return NoContent();
    }

    [HttpGet("api/v{version:apiVersion}/hrm/payroll-settings/rate-sets")]
    [ProducesResponseType(typeof(List<PayrollRateSetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRateSets(CancellationToken ct = default) =>
        Ok(await _mediator.Send(new ListRateSetsQuery(), ct));

    /// <summary>Create a rate set (no id) or change one (id): insurance rates, deductions, caps, annual leave and the tax brackets.</summary>
    [HttpPut("api/v{version:apiVersion}/hrm/payroll-settings/rate-sets")]
    [ProducesResponseType(typeof(PayrollRateSetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveRateSet([FromBody] SaveRateSetCommand command, CancellationToken ct = default) =>
        Ok(await _mediator.Send(command, ct));
}

public record SaveCompensationRequest(
    DateTime EffectiveFrom,
    decimal BaseSalary,
    decimal TaxableAllowance,
    decimal ExemptAllowance,
    decimal? InsuranceSalary,
    bool InsuranceEnabled,
    string? TaxCode,
    string? InsuranceNumber);

public record AddDependentRequest(string FullName, string Relationship, string? TaxCode, DateTime FromMonth, DateTime? ToMonth);
