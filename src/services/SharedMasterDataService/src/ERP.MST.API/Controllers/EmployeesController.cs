using Asp.Versioning;
using ERP.MST.Application.Commands;
using ERP.MST.Application.DTOs;
using ERP.MST.Application.Employees.Import;
using ERP.MST.Application.Queries;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Streaming;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MST.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/masterdata/employees")]
public class EmployeesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly UserContext _user;
    private readonly IImportProgressRegistry _imports;

    public EmployeesController(IMediator mediator, UserContext user, IImportProgressRegistry imports)
    {
        _mediator = mediator;
        _user = user;
        _imports = imports;
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
    /// Streaming import of employees. The body is the raw CSV (Content-Type text/csv), read row by row while it uploads; the answer is NDJSON,
    /// one line per row, flushed as it is processed (docs/architecture/streaming-import-pattern.md). Follow the progress on
    /// GET import/{importId}/events with the same importId. Rows that already exist or are invalid are skipped and reported.
    /// </summary>
    [HttpPost("import")]
    [AuthorizePermission("masterdata:employees:manage")]
    [RequestSizeLimit(30_000_000)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult Import([FromQuery] Guid importId, [FromQuery] bool dryRun = false)
    {
        if (importId == Guid.Empty)
            throw new BusinessRuleException("Import.Id", "importId is required: a new GUID made by the client.");

        return new NdjsonResult(_mediator.CreateStream(new ImportEmployeesStreamCommand
        {
            Body = Request.Body,
            ContentLength = Request.ContentLength,
            TenantId = _user.TenantId,
            UserId = _user.UserId,
            ImportId = importId,
            DryRun = dryRun,
        }));
    }

    /// <summary>Server-Sent Events with the progress of an import (events: progress, completed, failed). Only the user who runs the import sees it.</summary>
    [HttpGet("import/{importId:guid}/events")]
    [AuthorizePermission("masterdata:employees:manage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult ImportEvents(Guid importId)
    {
        var session = _imports.GetOrCreate(_user.TenantId, _user.UserId, importId);
        return new SseResult(session.SubscribeAsync(HttpContext.RequestAborted));
    }

    /// <summary>The CSV template of the import (UTF-8 with a BOM so Excel opens it correctly).</summary>
    [HttpGet("import/template")]
    [AuthorizePermission("masterdata:employees:manage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult ImportTemplate()
    {
        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(EmployeeImportParser.TemplateCsv)).ToArray();
        return File(bytes, "text/csv; charset=utf-8", "employees-import-template.csv");
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
