using ERP.Shared.Core.Domain;
using ERP.NTF.Domain.Enums;

namespace ERP.NTF.Domain.Entities;

public class NotificationPreference : AggregateRoot
{
    private readonly List<NotificationChannel> _disabledChannels = new();

    public Guid UserId { get; private set; }
    public bool EmailEnabled { get; private set; }
    public bool SmsEnabled { get; private set; }
    public bool PushEnabled { get; private set; }
    public bool InAppEnabled { get; private set; }
    public IReadOnlyCollection<NotificationChannel> DisabledChannels => _disabledChannels.AsReadOnly();
    public DateTime? OptOutAt { get; private set; }

    private NotificationPreference() { }

    public static NotificationPreference CreateDefault(Guid userId)
    {
        return new NotificationPreference
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EmailEnabled = true,
            SmsEnabled = true,
            PushEnabled = true,
            InAppEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateChannels(bool email, bool sms, bool push, bool inApp)
    {
        EmailEnabled = email;
        SmsEnabled = sms;
        PushEnabled = push;
        InAppEnabled = inApp;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsChannelEnabled(NotificationChannel channel) => channel switch
    {
        NotificationChannel.Email => EmailEnabled,
        NotificationChannel.Sms => SmsEnabled,
        NotificationChannel.Push => PushEnabled,
        NotificationChannel.InApp => InAppEnabled,
        _ => false
    };

    public void OptOut()
    {
        EmailEnabled = false;
        SmsEnabled = false;
        PushEnabled = false;
        OptOutAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
