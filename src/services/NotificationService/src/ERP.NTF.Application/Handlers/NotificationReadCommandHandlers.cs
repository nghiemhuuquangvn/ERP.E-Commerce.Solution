using ERP.NTF.Application.Commands;
using ERP.NTF.Application.Interfaces;
using MediatR;

namespace ERP.NTF.Application.Handlers;

public class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, bool>
{
    private readonly INotificationRepository _notifications;

    public MarkNotificationReadCommandHandler(INotificationRepository notifications) => _notifications = notifications;

    public async Task<bool> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await _notifications.GetByIdAsync(request.NotificationId, cancellationToken);
        if (notification == null) return false;

        notification.MarkAsRead();
        await _notifications.UpdateAsync(notification, cancellationToken);
        return true;
    }
}
