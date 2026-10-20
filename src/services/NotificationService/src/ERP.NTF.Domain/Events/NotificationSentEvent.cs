using ERP.Shared.Core.Domain;
using ERP.NTF.Domain.Enums;

namespace ERP.NTF.Domain.Events;

public record NotificationSentEvent(
    Guid NotificationId,
    string RecipientId,
    NotificationChannel Channel) : IDomainEvent, ITenantEvent
{
    /// <summary>Tenant the event belongs to (stamped by the Outbox writer).</summary>
    public Guid TenantId { get; set; }

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
