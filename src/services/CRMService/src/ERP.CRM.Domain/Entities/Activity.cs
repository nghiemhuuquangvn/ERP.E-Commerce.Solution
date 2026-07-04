using ERP.CRM.Domain.Enums;
using ERP.CRM.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.CRM.Domain.Entities;

public class Activity : AggregateRoot
{
    public Guid ContactId { get; private set; }
    public Guid? OpportunityId { get; private set; }
    public Guid? CaseId { get; private set; }
    public ActivityType ActivityType { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public ActivityStatus Status { get; private set; }
    public DateTime? ScheduledAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public string? Source { get; private set; }

    private Activity() { }

    public static Activity Create(
        Guid contactId,
        ActivityType activityType,
        string subject,
        string? notes = null,
        DateTime? scheduledAt = null,
        Guid? opportunityId = null,
        Guid? caseId = null,
        Guid? assignedToUserId = null,
        string? source = null)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new InvalidOperationException("Activity subject is required.");

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            ContactId = contactId,
            ActivityType = activityType,
            Subject = subject,
            Notes = notes,
            Status = scheduledAt.HasValue && scheduledAt > DateTime.UtcNow
                ? ActivityStatus.Scheduled
                : ActivityStatus.Completed,
            ScheduledAt = scheduledAt,
            CompletedAt = scheduledAt.HasValue && scheduledAt <= DateTime.UtcNow ? DateTime.UtcNow : null,
            OpportunityId = opportunityId,
            CaseId = caseId,
            AssignedToUserId = assignedToUserId,
            Source = source,
            CreatedAt = DateTime.UtcNow
        };

        activity.AddDomainEvent(new ActivityCreatedEvent(activity.Id, contactId, activityType.ToString(), subject));
        return activity;
    }

    public void Complete(string? notes = null)
    {
        if (Status == ActivityStatus.Completed)
            throw new InvalidOperationException("Activity is already completed.");

        if (Status == ActivityStatus.Cancelled)
            throw new InvalidOperationException("Cannot complete a cancelled activity.");

        Status = ActivityStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(notes)) Notes = notes;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new ActivityCompletedEvent(Id, ContactId));
    }

    public void Cancel()
    {
        if (Status == ActivityStatus.Completed)
            throw new InvalidOperationException("Cannot cancel a completed activity.");

        Status = ActivityStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateNotes(string notes)
    {
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;
    }
}
