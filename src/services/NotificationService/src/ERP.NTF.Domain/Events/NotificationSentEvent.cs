using ERP.Shared.Core.Domain;
using ERP.NTF.Domain.Enums;

namespace ERP.NTF.Domain.Events;

public record NotificationSentEvent(
    Guid NotificationId,
    string RecipientId,
    NotificationChannel Channel) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
