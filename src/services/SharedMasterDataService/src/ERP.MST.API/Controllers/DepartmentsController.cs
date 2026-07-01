using ERP.MST.Application.Commands;
using ERP.MST.Application.DTOs;
using ERP.MST.Application.Queries;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MST.API.Controllers;

[ApiController]
[Route("api/v1/masterdata/departments")]
public class DepartmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DepartmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List all departments (paginated).
    /// </summary>
    [HttpGet]
    [AuthorizePermission("masterdata:departments:view")]
    [ProducesResponseType(typeof(PagedResult<DepartmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] string? languageCode = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListDepartmentsQuery(languageCode, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new department.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("masterdata:departments:manage")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateDepartmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get a department by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("masterdata:departments:view")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        [FromQuery] string? languageCode = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetDepartmentQuery(id, languageCode), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Department {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Update an existing department.
    /// </summary>
    [HttpPut("{id:guid}")]
    [AuthorizePermission("masterdata:departments:manage")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateDepartmentCommand(id, request.Name, request.Code, request.ManagerId);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}

public record UpdateDepartmentRequest(string Name, string Code, Guid? ManagerId);
