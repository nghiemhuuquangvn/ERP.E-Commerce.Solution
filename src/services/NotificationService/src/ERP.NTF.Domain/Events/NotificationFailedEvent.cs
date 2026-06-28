using ERP.Shared.Core.Domain;
using ERP.NTF.Domain.Enums;

namespace ERP.NTF.Domain.Events;

public record NotificationFailedEvent(
    Guid NotificationId,
    string RecipientId,
    NotificationChannel Channel,
    string ErrorMessage) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
