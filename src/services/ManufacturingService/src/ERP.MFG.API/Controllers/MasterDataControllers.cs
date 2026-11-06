using Asp.Versioning;
using ERP.MFG.Application.Features.Calendars;
using ERP.MFG.Application.Features.Crews;
using ERP.MFG.Application.Features.WorkCenters;
using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.MFG.API.Controllers;

// Master data of production (BA spec 01 §5.4-5.7). Reading needs manufacturing:masterdata:view, changing manufacturing:masterdata:manage;
// every plant-scoped action also checks that the caller may see the plant (BR-GEN-09: a foreign plant is a 404).

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/shifts")]
public class ShiftsController(IMediator mediator) : ControllerBase
{
    public sealed record ShiftBody(string Name, TimeOnly StartTime, TimeOnly EndTime, int BreakMinutes);
    public sealed record CreateShiftBody(Guid PlantId, string Code, string Name, TimeOnly StartTime, TimeOnly EndTime, int BreakMinutes);

    [HttpGet]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataView)]
    public async Task<IActionResult> List([FromQuery] Guid plantId, CancellationToken ct) => Ok(await mediator.Send(new GetShiftsQuery(plantId), ct));

    [HttpPost]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Create([FromBody] CreateShiftBody b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new CreateShiftCommand(b.PlantId, b.Code, b.Name, b.StartTime, b.EndTime, b.BreakMinutes), ct));

    [HttpPut("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ShiftBody b, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateShiftCommand(id, b.Name, b.StartTime, b.EndTime, b.BreakMinutes), ct));

    [HttpPost("{id:guid}/activate")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) => Ok(await mediator.Send(new SetShiftActiveCommand(id, true), ct));

    [HttpPost("{id:guid}/deactivate")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) => Ok(await mediator.Send(new SetShiftActiveCommand(id, false), ct));

    [HttpDelete("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await mediator.Send(new DeleteShiftCommand(id), ct); return NoContent(); }
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/calendars")]
public class CalendarsController(IMediator mediator) : ControllerBase
{
    public sealed record CreateBody(Guid PlantId, string Code, string Name);
    public sealed record RenameBody(string Name);
    public sealed record DayBody(List<Guid> ShiftIds);
    public sealed record ExceptionBody(DateOnly Date, CalendarExceptionType Type, string? Description, List<Guid>? ShiftIds);

    [HttpGet]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataView)]
    public async Task<IActionResult> List([FromQuery] Guid plantId, CancellationToken ct) => Ok(await mediator.Send(new GetCalendarsQuery(plantId), ct));

    [HttpGet("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataView)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await mediator.Send(new GetCalendarQuery(id), ct));

    [HttpPost]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Create([FromBody] CreateBody b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new CreateCalendarCommand(b.PlantId, b.Code, b.Name), ct));

    [HttpPut("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Rename(Guid id, [FromBody] RenameBody b, CancellationToken ct) => Ok(await mediator.Send(new RenameCalendarCommand(id, b.Name), ct));

    /// <summary>Replaces the shifts of one weekday of the weekly pattern (an empty list = a day off).</summary>
    [HttpPut("{id:guid}/days/{dayOfWeek}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> SetDay(Guid id, DayOfWeek dayOfWeek, [FromBody] DayBody b, CancellationToken ct)
        => Ok(await mediator.Send(new SetCalendarDayCommand(id, dayOfWeek, b.ShiftIds ?? []), ct));

    [HttpPost("{id:guid}/exceptions")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> AddException(Guid id, [FromBody] ExceptionBody b, CancellationToken ct)
        => Ok(await mediator.Send(new AddCalendarExceptionCommand(id, b.Date, b.Type, b.Description, b.ShiftIds), ct));

    [HttpDelete("{id:guid}/exceptions/{date}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> RemoveException(Guid id, DateOnly date, CancellationToken ct) => Ok(await mediator.Send(new RemoveCalendarExceptionCommand(id, date), ct));

    [HttpPost("{id:guid}/activate")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) => Ok(await mediator.Send(new SetCalendarActiveCommand(id, true), ct));

    [HttpPost("{id:guid}/deactivate")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) => Ok(await mediator.Send(new SetCalendarActiveCommand(id, false), ct));

    [HttpDelete("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await mediator.Send(new DeleteCalendarCommand(id), ct); return NoContent(); }

    /// <summary>The calendar view of S04: working minutes of every day of a month.</summary>
    [HttpGet("{id:guid}/month")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataView)]
    public async Task<IActionResult> Month(Guid id, [FromQuery] int year, [FromQuery] int month, CancellationToken ct) => Ok(await mediator.Send(new GetCalendarMonthQuery(id, year, month), ct));

    /// <summary>Working-time arithmetic of the calendar: addMinutes / subtractMinutes from (date, workingMinute), or addDays / subtractDays from a date.</summary>
    [HttpGet("{id:guid}/working-time")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataView)]
    public async Task<IActionResult> WorkingTime(Guid id, [FromQuery] WorkingTimeOperation operation, [FromQuery] DateOnly date, [FromQuery] int workingMinute = 0, [FromQuery] int amount = 0, CancellationToken ct = default)
        => Ok(await mediator.Send(new WorkingTimeQuery(id, operation, date, workingMinute, amount), ct));
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/work-centers")]
public class WorkCentersController(IMediator mediator) : ControllerBase
{
    public sealed record CreateBody(Guid PlantId, string Code, WorkCenterValues Values);

    [HttpGet]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataView)]
    public async Task<IActionResult> List([FromQuery] Guid plantId, CancellationToken ct) => Ok(await mediator.Send(new GetWorkCentersQuery(plantId), ct));

    [HttpGet("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataView)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await mediator.Send(new GetWorkCenterQuery(id), ct));

    [HttpPost]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Create([FromBody] CreateBody b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new CreateWorkCenterCommand(b.PlantId, b.Code, b.Values), ct));

    [HttpPut("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] WorkCenterValues values, CancellationToken ct) => Ok(await mediator.Send(new UpdateWorkCenterCommand(id, values), ct));

    [HttpPost("{id:guid}/activate")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) => Ok(await mediator.Send(new SetWorkCenterActiveCommand(id, true), ct));

    [HttpPost("{id:guid}/deactivate")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) => Ok(await mediator.Send(new SetWorkCenterActiveCommand(id, false), ct));

    [HttpDelete("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await mediator.Send(new DeleteWorkCenterCommand(id), ct); return NoContent(); }

    /// <summary>The capacity chart of S05: per day the working minutes and the standard-minute capacity (BR-WC-02).</summary>
    [HttpGet("{id:guid}/capacity")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataView)]
    public async Task<IActionResult> Capacity(Guid id, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) => Ok(await mediator.Send(new GetCapacityQuery(id, from, to), ct));
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/machines")]
public class MachinesController(IMediator mediator) : ControllerBase
{
    public sealed record CreateBody(Guid PlantId, string Code, string Name, Guid WorkCenterId, decimal IdealCycleTimeSeconds, string? SerialNo);
    public sealed record UpdateBody(string Name, Guid WorkCenterId, decimal IdealCycleTimeSeconds, string? SerialNo);
    public sealed record StatusBody(MachineStatus Status);

    [HttpGet]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataView)]
    public async Task<IActionResult> List([FromQuery] Guid plantId, [FromQuery] Guid? workCenterId, CancellationToken ct) => Ok(await mediator.Send(new GetMachinesQuery(plantId, workCenterId), ct));

    [HttpPost]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Create([FromBody] CreateBody b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new CreateMachineCommand(b.PlantId, b.Code, b.Name, b.WorkCenterId, b.IdealCycleTimeSeconds, b.SerialNo), ct));

    [HttpPut("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBody b, CancellationToken ct) => Ok(await mediator.Send(new UpdateMachineCommand(id, b.Name, b.WorkCenterId, b.IdealCycleTimeSeconds, b.SerialNo), ct));

    /// <summary>Active, UnderMaintenance or Retired (BR-MCH-01).</summary>
    [HttpPut("{id:guid}/status")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] StatusBody b, CancellationToken ct) => Ok(await mediator.Send(new SetMachineStatusCommand(id, b.Status), ct));

    [HttpPost("{id:guid}/activate")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) => Ok(await mediator.Send(new SetMachineActiveCommand(id, true), ct));

    [HttpPost("{id:guid}/deactivate")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) => Ok(await mediator.Send(new SetMachineActiveCommand(id, false), ct));

    [HttpDelete("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await mediator.Send(new DeleteMachineCommand(id), ct); return NoContent(); }
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/standard-operations")]
public class StandardOperationsController(IMediator mediator) : ControllerBase
{
    public sealed record CreateBody(Guid PlantId, string Code, StandardOperationBody Body);

    [HttpGet]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataView)]
    public async Task<IActionResult> List([FromQuery] Guid plantId, CancellationToken ct) => Ok(await mediator.Send(new GetStandardOperationsQuery(plantId), ct));

    [HttpPost]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Create([FromBody] CreateBody b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new CreateStandardOperationCommand(b.PlantId, b.Code, b.Body), ct));

    [HttpPut("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] StandardOperationBody body, CancellationToken ct) => Ok(await mediator.Send(new UpdateStandardOperationCommand(id, body), ct));

    [HttpPost("{id:guid}/activate")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) => Ok(await mediator.Send(new SetStandardOperationActiveCommand(id, true), ct));

    [HttpPost("{id:guid}/deactivate")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) => Ok(await mediator.Send(new SetStandardOperationActiveCommand(id, false), ct));

    [HttpDelete("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await mediator.Send(new DeleteStandardOperationCommand(id), ct); return NoContent(); }
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/reason-codes")]
public class ReasonCodesController(IMediator mediator) : ControllerBase
{
    public sealed record UpdateBody(string Name, DowntimeCategory? Category);

    /// <summary>The tenant's reason codes; a tenant that has none yet receives the default set on this first read.</summary>
    [HttpGet]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataView)]
    public async Task<IActionResult> List([FromQuery] ReasonType? type, CancellationToken ct) => Ok(await mediator.Send(new GetReasonCodesQuery(type), ct));

    /// <summary>Adds the default reasons the tenant does not have yet (idempotent); returns how many were added.</summary>
    [HttpPost("seed-defaults")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Seed(CancellationToken ct) => Ok(new { added = await mediator.Send(new SeedReasonCodesCommand(), ct) });

    [HttpPost]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Create([FromBody] CreateReasonCodeCommand command, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBody b, CancellationToken ct) => Ok(await mediator.Send(new UpdateReasonCodeCommand(id, b.Name, b.Category), ct));

    [HttpPost("{id:guid}/activate")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) => Ok(await mediator.Send(new SetReasonCodeActiveCommand(id, true), ct));

    [HttpPost("{id:guid}/deactivate")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) => Ok(await mediator.Send(new SetReasonCodeActiveCommand(id, false), ct));

    [HttpDelete("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await mediator.Send(new DeleteReasonCodeCommand(id), ct); return NoContent(); }
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/manufacturing/crews")]
public class CrewsController(IMediator mediator) : ControllerBase
{
    public sealed record CreateBody(Guid PlantId, string Code, string Name, Guid? WorkCenterId, Guid? LeaderEmployeeId);
    public sealed record UpdateBody(string Name, Guid? WorkCenterId, Guid? LeaderEmployeeId);
    public sealed record MemberBody(Guid EmployeeId, DateOnly ValidFrom, DateOnly? ValidTo, decimal? LaborRatePerHour);

    [HttpGet]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataView)]
    public async Task<IActionResult> List([FromQuery] Guid plantId, CancellationToken ct) => Ok(await mediator.Send(new GetCrewsQuery(plantId), ct));

    [HttpPost]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Create([FromBody] CreateBody b, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(new CreateCrewCommand(b.PlantId, b.Code, b.Name, b.WorkCenterId, b.LeaderEmployeeId), ct));

    [HttpPut("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBody b, CancellationToken ct) => Ok(await mediator.Send(new UpdateCrewCommand(id, b.Name, b.WorkCenterId, b.LeaderEmployeeId), ct));

    [HttpPost("{id:guid}/activate")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) => Ok(await mediator.Send(new SetCrewActiveCommand(id, true), ct));

    [HttpPost("{id:guid}/deactivate")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) => Ok(await mediator.Send(new SetCrewActiveCommand(id, false), ct));

    [HttpDelete("{id:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await mediator.Send(new DeleteCrewCommand(id), ct); return NoContent(); }

    [HttpPost("{id:guid}/members")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] MemberBody b, CancellationToken ct)
        => Ok(await mediator.Send(new AddCrewMemberCommand(id, b.EmployeeId, b.ValidFrom, b.ValidTo, b.LaborRatePerHour), ct));

    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    [AuthorizePermission(PermissionConstants.Manufacturing_MasterDataManage)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId, CancellationToken ct) => Ok(await mediator.Send(new RemoveCrewMemberCommand(id, memberId), ct));
}
