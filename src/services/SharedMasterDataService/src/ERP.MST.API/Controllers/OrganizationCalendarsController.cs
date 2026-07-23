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
[Route("api/v{version:apiVersion}/masterdata/organization-calendars")]
public class OrganizationCalendarsController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrganizationCalendarsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ─── Calendars ──────────────────────────────────────────────────────────

    /// <summary>
    /// List organization calendars (paginated), optionally filtered by company.
    /// </summary>
    [HttpGet]
    [AuthorizePermission("masterdata:organization-calendars:view")]
    [ProducesResponseType(typeof(PagedResult<OrganizationCalendarDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListOrganizationCalendarsQuery(companyId, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new organization calendar.
    /// </summary>
    [HttpPost]
    [AuthorizePermission("masterdata:organization-calendars:manage")]
    [ProducesResponseType(typeof(OrganizationCalendarDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrganizationCalendarCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get an organization calendar by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AuthorizePermission("masterdata:organization-calendars:view")]
    [ProducesResponseType(typeof(OrganizationCalendarDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetOrganizationCalendarQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"OrganizationCalendar {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Update an existing organization calendar.
    /// </summary>
    [HttpPut("{id:guid}")]
    [AuthorizePermission("masterdata:organization-calendars:manage")]
    [ProducesResponseType(typeof(OrganizationCalendarDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateOrganizationCalendarRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateOrganizationCalendarCommand(id, request.Name, request.IsDefault);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    // ─── Holidays ───────────────────────────────────────────────────────────

    [HttpGet("{calendarId:guid}/holidays")]
    [AuthorizePermission("masterdata:organization-calendars:view")]
    [ProducesResponseType(typeof(PagedResult<HolidayDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListHolidays(
        [FromRoute] Guid calendarId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListHolidaysQuery(calendarId, page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{calendarId:guid}/holidays")]
    [AuthorizePermission("masterdata:organization-calendars:manage")]
    [ProducesResponseType(typeof(HolidayDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateHoliday(
        [FromRoute] Guid calendarId,
        [FromBody] CreateHolidayRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateHolidayCommand(calendarId, request.Date, request.Name, request.IsRecurringYearly);
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetHoliday), new { calendarId, id = result.Id }, result);
    }

    [HttpGet("{calendarId:guid}/holidays/{id:guid}")]
    [AuthorizePermission("masterdata:organization-calendars:view")]
    [ProducesResponseType(typeof(HolidayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetHoliday(
        [FromRoute] Guid calendarId,
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetHolidayQuery(id), cancellationToken);
        if (result is null)
            return NotFound(new { message = $"Holiday {id} not found." });

        return Ok(result);
    }

    [HttpPut("{calendarId:guid}/holidays/{id:guid}")]
    [AuthorizePermission("masterdata:organization-calendars:manage")]
    [ProducesResponseType(typeof(HolidayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateHoliday(
        [FromRoute] Guid calendarId,
        [FromRoute] Guid id,
        [FromBody] UpdateHolidayRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateHolidayCommand(id, request.Date, request.Name, request.IsRecurringYearly);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}

public record UpdateOrganizationCalendarRequest(string Name, bool IsDefault);
public record CreateHolidayRequest(DateTime Date, string Name, bool IsRecurringYearly = false);
public record UpdateHolidayRequest(DateTime Date, string Name, bool IsRecurringYearly);
