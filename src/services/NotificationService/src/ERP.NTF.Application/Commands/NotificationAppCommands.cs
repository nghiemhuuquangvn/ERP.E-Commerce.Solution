using MediatR;

namespace ERP.NTF.Application.Commands;

/// <summary>Marks every unread notification of the signed-in user as read; answers how many there were.</summary>
public class MarkAllNotificationsReadCommand : IRequest<int>
{
    public string RecipientId { get; set; } = string.Empty;
}

/// <summary>A phone registers (or refreshes) its push token for the signed-in user. The same device registering again replaces its token.</summary>
public class RegisterDeviceTokenCommand : IRequest<Unit>
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string? Platform { get; set; }
}

/// <summary>The phone signs out: its token is removed so the next user of the account does not get this user's messages.</summary>
public class UnregisterDeviceTokenCommand : IRequest<bool>
{
    public Guid UserId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
}
