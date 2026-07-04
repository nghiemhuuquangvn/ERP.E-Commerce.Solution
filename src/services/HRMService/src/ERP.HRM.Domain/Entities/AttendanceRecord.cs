using ERP.HRM.Domain.Enums;
using ERP.HRM.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.HRM.Domain.Entities;

/// <summary>
/// Tracks a single employee's daily attendance: check-in, check-out, total hours, overtime.
/// One record per employee per date — enforced by a unique index in the DB.
/// </summary>
public class AttendanceRecord : AggregateRoot
{
    private const decimal StandardShiftHours = 8m;
    private const double LateGraceMinutes = 15.0;

    public Guid EmployeeId { get; private set; }
    public DateTime Date { get; private set; }
    public DateTime? CheckInTime { get; private set; }
    public DateTime? CheckOutTime { get; private set; }
    public decimal TotalHours { get; private set; }
    public decimal OvertimeHours { get; private set; }
    public AttendanceStatus Status { get; private set; }
    public Guid? WorkShiftId { get; private set; }
    public string? Notes { get; private set; }

    private AttendanceRecord() { }

    public static AttendanceRecord CheckIn(
        Guid employeeId,
        DateTime checkInTime,
        Guid? workShiftId = null,
        TimeSpan? shiftStartTime = null,
        string? notes = null)
    {
        var date = checkInTime.Date;

        var isLate = shiftStartTime.HasValue
            && checkInTime.TimeOfDay > shiftStartTime.Value.Add(TimeSpan.FromMinutes(LateGraceMinutes));

        var record = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            EmployeeId = employeeId,
            Date = date,
            CheckInTime = checkInTime,
            Status = isLate ? AttendanceStatus.Late : AttendanceStatus.Present,
            WorkShiftId = workShiftId,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        record.AddDomainEvent(new AttendanceCheckedInEvent(record.Id, employeeId, date, checkInTime));
        return record;
    }

    public void CheckOut(DateTime checkOutTime, string? notes = null)
    {
        if (CheckInTime is null)
            throw new InvalidOperationException("Cannot check out without a check-in.");

        if (checkOutTime <= CheckInTime)
            throw new InvalidOperationException("Check-out time must be after check-in time.");

        CheckOutTime = checkOutTime;
        TotalHours = Math.Round((decimal)(checkOutTime - CheckInTime.Value).TotalHours, 2);
        OvertimeHours = Math.Max(0m, TotalHours - StandardShiftHours);

        if (TotalHours < StandardShiftHours / 2)
            Status = AttendanceStatus.HalfDay;

        if (notes is not null)
            Notes = notes;

        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new AttendanceCheckedOutEvent(Id, EmployeeId, checkOutTime, TotalHours, OvertimeHours));
    }

    public void MarkAbsent(string? notes = null)
    {
        Status = AttendanceStatus.Absent;
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkOnLeave(string? notes = null)
    {
        Status = AttendanceStatus.OnLeave;
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkHoliday()
    {
        Status = AttendanceStatus.Holiday;
        UpdatedAt = DateTime.UtcNow;
    }
}
