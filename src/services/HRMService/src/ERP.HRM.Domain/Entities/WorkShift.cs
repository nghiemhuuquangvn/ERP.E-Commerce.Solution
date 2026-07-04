using ERP.Shared.Core.Domain;

namespace ERP.HRM.Domain.Entities;

/// <summary>
/// Defines a named work schedule (e.g. "Morning 08:00–17:00").
/// Referenced by AttendanceRecord to determine lateness and standard hours.
/// </summary>
public class WorkShift : AggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public TimeSpan StartTime { get; private set; }
    public TimeSpan EndTime { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Duration in hours between StartTime and EndTime (exclusive of breaks).</summary>
    public decimal ShiftHours => (decimal)(EndTime - StartTime).TotalHours;

    private WorkShift() { }

    public static WorkShift Create(string name, TimeSpan startTime, TimeSpan endTime)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Shift name is required.");

        if (endTime <= startTime)
            throw new InvalidOperationException("Shift end time must be after start time.");

        return new WorkShift
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            StartTime = startTime,
            EndTime = endTime,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, TimeSpan startTime, TimeSpan endTime)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Shift name is required.");

        if (endTime <= startTime)
            throw new InvalidOperationException("Shift end time must be after start time.");

        Name = name.Trim();
        StartTime = startTime;
        EndTime = endTime;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
