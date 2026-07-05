using ERP.HRM.Application.Commands;
using ERP.HRM.Application.DTOs;
using ERP.HRM.Application.Interfaces;
using ERP.HRM.Application.Queries;
using ERP.HRM.Domain.Entities;
using ERP.HRM.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.HRM.Application.Handlers;

// ─── Employee Snapshot Query Handlers ─────────────────────────────────────────

public class GetEmployeeSnapshotQueryHandler : IRequestHandler<GetEmployeeSnapshotQuery, EmployeeSnapshotDto?>
{
    private readonly IEmployeeSnapshotRepository _repository;
    public GetEmployeeSnapshotQueryHandler(IEmployeeSnapshotRepository repository) => _repository = repository;

    public async Task<EmployeeSnapshotDto?> Handle(GetEmployeeSnapshotQuery request, CancellationToken cancellationToken)
    {
        var snapshot = await _repository.GetByIdAsync(request.EmployeeId, cancellationToken);
        return snapshot is null ? null : MapToDto(snapshot);
    }

    internal static EmployeeSnapshotDto MapToDto(EmployeeSnapshot s) =>
        new(s.Id, s.EmployeeNumber, s.FullName, s.Email,
            ((EmploymentStatus)s.EmploymentStatus).ToString(), s.DepartmentId);
}

public class ListEmployeeSnapshotsQueryHandler : IRequestHandler<ListEmployeeSnapshotsQuery, PagedResult<EmployeeSnapshotDto>>
{
    private readonly IEmployeeSnapshotRepository _repository;
    public ListEmployeeSnapshotsQueryHandler(IEmployeeSnapshotRepository repository) => _repository = repository;

    public async Task<PagedResult<EmployeeSnapshotDto>> Handle(ListEmployeeSnapshotsQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.Search, request.DepartmentId, request.EmploymentStatus,
            request.Page, request.PageSize, cancellationToken);

        return new PagedResult<EmployeeSnapshotDto>(
            items.Select(GetEmployeeSnapshotQueryHandler.MapToDto).ToList(),
            total, request.Page, request.PageSize);
    }
}

// ─── Attendance Command Handlers ──────────────────────────────────────────────

public class CheckInCommandHandler : IRequestHandler<CheckInCommand, AttendanceDto>
{
    private readonly IHrmUnitOfWork _uow;
    private readonly ILogger<CheckInCommandHandler> _logger;

    public CheckInCommandHandler(
        IHrmUnitOfWork uow,
        ILogger<CheckInCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<AttendanceDto> Handle(CheckInCommand request, CancellationToken cancellationToken)
    {
        var existing = await _uow.Attendance.GetByEmployeeAndDateAsync(
            request.EmployeeId, request.CheckInTime.Date, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException(
                $"Employee {request.EmployeeId} already has an attendance record for {request.CheckInTime:yyyy-MM-dd}.");

        TimeSpan? shiftStart = null;
        if (request.WorkShiftId.HasValue)
        {
            var shift = await _uow.WorkShifts.GetByIdAsync(request.WorkShiftId.Value, cancellationToken);
            shiftStart = shift?.StartTime;
        }

        var record = AttendanceRecord.CheckIn(
            request.EmployeeId,
            request.CheckInTime,
            request.WorkShiftId,
            shiftStart,
            request.Notes);

        _uow.Attendance.Add(record);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee {EmployeeId} checked in at {Time}", request.EmployeeId, request.CheckInTime);
        return MapToDto(record);
    }

    internal static AttendanceDto MapToDto(AttendanceRecord r) =>
        new(r.Id, r.EmployeeId, r.Date, r.CheckInTime, r.CheckOutTime,
            r.TotalHours, r.OvertimeHours,
            ((AttendanceStatus)r.Status).ToString(),
            r.WorkShiftId, r.Notes, r.CreatedAt);
}

public class CheckOutCommandHandler : IRequestHandler<CheckOutCommand, AttendanceDto>
{
    private readonly IHrmUnitOfWork _uow;
    private readonly ILogger<CheckOutCommandHandler> _logger;

    public CheckOutCommandHandler(IHrmUnitOfWork uow, ILogger<CheckOutCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<AttendanceDto> Handle(CheckOutCommand request, CancellationToken cancellationToken)
    {
        var record = await _uow.Attendance.GetByIdAsync(request.AttendanceRecordId, cancellationToken)
            ?? throw new KeyNotFoundException($"Attendance record {request.AttendanceRecordId} not found.");

        if (record.CheckOutTime.HasValue)
            throw new InvalidOperationException("Already checked out.");

        record.CheckOut(request.CheckOutTime, request.Notes);
        _uow.Attendance.Update(record);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Employee {EmployeeId} checked out at {Time} — {Hours:F2}h worked ({OT:F2}h OT)",
            record.EmployeeId, request.CheckOutTime, record.TotalHours, record.OvertimeHours);

        return CheckInCommandHandler.MapToDto(record);
    }
}

public class MarkAbsentCommandHandler : IRequestHandler<MarkAbsentCommand, AttendanceDto>
{
    private readonly IHrmUnitOfWork _uow;
    private readonly ILogger<MarkAbsentCommandHandler> _logger;

    public MarkAbsentCommandHandler(IHrmUnitOfWork uow, ILogger<MarkAbsentCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<AttendanceDto> Handle(MarkAbsentCommand request, CancellationToken cancellationToken)
    {
        var existing = await _uow.Attendance.GetByEmployeeAndDateAsync(request.EmployeeId, request.Date, cancellationToken);
        if (existing is not null)
        {
            existing.MarkAbsent(request.Notes);
            _uow.Attendance.Update(existing);
            await _uow.SaveChangesAsync(cancellationToken);
            return CheckInCommandHandler.MapToDto(existing);
        }

        var record = AttendanceRecord.CheckIn(request.EmployeeId, request.Date, notes: request.Notes);
        record.MarkAbsent(request.Notes);
        _uow.Attendance.Add(record);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee {EmployeeId} marked absent for {Date:yyyy-MM-dd}", request.EmployeeId, request.Date);
        return CheckInCommandHandler.MapToDto(record);
    }
}

// ─── Attendance Query Handlers ────────────────────────────────────────────────

public class GetAttendanceQueryHandler : IRequestHandler<GetAttendanceQuery, AttendanceDto?>
{
    private readonly IAttendanceRepository _attendance;
    public GetAttendanceQueryHandler(IAttendanceRepository attendance) => _attendance = attendance;

    public async Task<AttendanceDto?> Handle(GetAttendanceQuery request, CancellationToken cancellationToken)
    {
        var record = await _attendance.GetByIdAsync(request.AttendanceRecordId, cancellationToken);
        return record is null ? null : CheckInCommandHandler.MapToDto(record);
    }
}

public class GetAttendanceByDateQueryHandler : IRequestHandler<GetAttendanceByDateQuery, AttendanceDto?>
{
    private readonly IAttendanceRepository _attendance;
    public GetAttendanceByDateQueryHandler(IAttendanceRepository attendance) => _attendance = attendance;

    public async Task<AttendanceDto?> Handle(GetAttendanceByDateQuery request, CancellationToken cancellationToken)
    {
        var record = await _attendance.GetByEmployeeAndDateAsync(request.EmployeeId, request.Date, cancellationToken);
        return record is null ? null : CheckInCommandHandler.MapToDto(record);
    }
}

public class ListAttendanceQueryHandler : IRequestHandler<ListAttendanceQuery, PagedResult<AttendanceDto>>
{
    private readonly IAttendanceRepository _attendance;
    public ListAttendanceQueryHandler(IAttendanceRepository attendance) => _attendance = attendance;

    public async Task<PagedResult<AttendanceDto>> Handle(ListAttendanceQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _attendance.ListAsync(
            request.EmployeeId, request.FromDate, request.ToDate, request.Status,
            request.Page, request.PageSize, cancellationToken);

        return new PagedResult<AttendanceDto>(
            items.Select(CheckInCommandHandler.MapToDto).ToList(),
            total, request.Page, request.PageSize);
    }
}

public class GetAttendanceSummaryQueryHandler : IRequestHandler<GetAttendanceSummaryQuery, AttendanceSummaryDto>
{
    private readonly IAttendanceRepository _attendance;
    public GetAttendanceSummaryQueryHandler(IAttendanceRepository attendance) => _attendance = attendance;

    public async Task<AttendanceSummaryDto> Handle(GetAttendanceSummaryQuery request, CancellationToken cancellationToken)
    {
        var startDate = new DateTime(request.Year, request.Month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var (items, _) = await _attendance.ListAsync(
            request.EmployeeId, startDate, endDate, null, 1, 1000, cancellationToken);

        return new AttendanceSummaryDto(
            EmployeeId: request.EmployeeId,
            Year: request.Year,
            Month: request.Month,
            PresentDays: items.Count(r => r.Status is AttendanceStatus.Present or AttendanceStatus.Late),
            AbsentDays: items.Count(r => r.Status == AttendanceStatus.Absent),
            LateDays: items.Count(r => r.Status == AttendanceStatus.Late),
            LeaveDays: items.Count(r => r.Status == AttendanceStatus.OnLeave),
            TotalHours: items.Sum(r => r.TotalHours),
            TotalOvertimeHours: items.Sum(r => r.OvertimeHours));
    }
}

// ─── Work Shift Command Handlers ──────────────────────────────────────────────

public class CreateWorkShiftCommandHandler : IRequestHandler<CreateWorkShiftCommand, WorkShiftDto>
{
    private readonly IHrmUnitOfWork _uow;
    private readonly ILogger<CreateWorkShiftCommandHandler> _logger;

    public CreateWorkShiftCommandHandler(IHrmUnitOfWork uow, ILogger<CreateWorkShiftCommandHandler> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<WorkShiftDto> Handle(CreateWorkShiftCommand request, CancellationToken cancellationToken)
    {
        var start = TimeSpan.Parse(request.StartTime);
        var end = TimeSpan.Parse(request.EndTime);

        var shift = WorkShift.Create(request.Name, start, end);
        _uow.WorkShifts.Add(shift);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Work shift '{Name}' created: {Id}", shift.Name, shift.Id);
        return MapToDto(shift);
    }

    internal static WorkShiftDto MapToDto(WorkShift s) =>
        new(s.Id, s.Name, s.StartTime.ToString(@"hh\:mm"), s.EndTime.ToString(@"hh\:mm"),
            s.ShiftHours, s.IsActive, s.CreatedAt);
}

public class UpdateWorkShiftCommandHandler : IRequestHandler<UpdateWorkShiftCommand, WorkShiftDto>
{
    private readonly IHrmUnitOfWork _uow;

    public UpdateWorkShiftCommandHandler(IHrmUnitOfWork uow) => _uow = uow;

    public async Task<WorkShiftDto> Handle(UpdateWorkShiftCommand request, CancellationToken cancellationToken)
    {
        var shift = await _uow.WorkShifts.GetByIdAsync(request.ShiftId, cancellationToken)
            ?? throw new KeyNotFoundException($"Work shift {request.ShiftId} not found.");

        shift.Update(request.Name, TimeSpan.Parse(request.StartTime), TimeSpan.Parse(request.EndTime));
        _uow.WorkShifts.Update(shift);
        await _uow.SaveChangesAsync(cancellationToken);

        return CreateWorkShiftCommandHandler.MapToDto(shift);
    }
}

public class DeactivateWorkShiftCommandHandler : IRequestHandler<DeactivateWorkShiftCommand, WorkShiftDto>
{
    private readonly IHrmUnitOfWork _uow;

    public DeactivateWorkShiftCommandHandler(IHrmUnitOfWork uow) => _uow = uow;

    public async Task<WorkShiftDto> Handle(DeactivateWorkShiftCommand request, CancellationToken cancellationToken)
    {
        var shift = await _uow.WorkShifts.GetByIdAsync(request.ShiftId, cancellationToken)
            ?? throw new KeyNotFoundException($"Work shift {request.ShiftId} not found.");

        shift.Deactivate();
        _uow.WorkShifts.Update(shift);
        await _uow.SaveChangesAsync(cancellationToken);

        return CreateWorkShiftCommandHandler.MapToDto(shift);
    }
}

// ─── Work Shift Query Handlers ────────────────────────────────────────────────

public class GetWorkShiftQueryHandler : IRequestHandler<GetWorkShiftQuery, WorkShiftDto?>
{
    private readonly IWorkShiftRepository _shifts;
    public GetWorkShiftQueryHandler(IWorkShiftRepository shifts) => _shifts = shifts;

    public async Task<WorkShiftDto?> Handle(GetWorkShiftQuery request, CancellationToken cancellationToken)
    {
        var shift = await _shifts.GetByIdAsync(request.ShiftId, cancellationToken);
        return shift is null ? null : CreateWorkShiftCommandHandler.MapToDto(shift);
    }
}

public class ListWorkShiftsQueryHandler : IRequestHandler<ListWorkShiftsQuery, IEnumerable<WorkShiftDto>>
{
    private readonly IWorkShiftRepository _shifts;
    public ListWorkShiftsQueryHandler(IWorkShiftRepository shifts) => _shifts = shifts;

    public async Task<IEnumerable<WorkShiftDto>> Handle(ListWorkShiftsQuery request, CancellationToken cancellationToken)
    {
        var shifts = await _shifts.GetAllAsync(request.ActiveOnly, cancellationToken);
        return shifts.Select(CreateWorkShiftCommandHandler.MapToDto);
    }
}
