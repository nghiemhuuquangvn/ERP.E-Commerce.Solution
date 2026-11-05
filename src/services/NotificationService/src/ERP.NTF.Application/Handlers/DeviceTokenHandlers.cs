using ERP.NTF.Application.Commands;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.NTF.Application.Handlers;

public class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand, int>
{
    private readonly INotificationRepository _notifications;

    public MarkAllNotificationsReadCommandHandler(INotificationRepository notifications) => _notifications = notifications;

    public async Task<int> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RecipientId)) return 0;
        return await _notifications.MarkAllReadAsync(request.RecipientId, cancellationToken);
    }
}

public class RegisterDeviceTokenCommandHandler : IRequestHandler<RegisterDeviceTokenCommand, Unit>
{
    public const int MaxTokenLength = 500;
    public const int MaxDeviceIdLength = 200;
    private static readonly string[] Platforms = { "android", "ios" };

    private readonly IDeviceTokenRepository _tokens;

    public RegisterDeviceTokenCommandHandler(IDeviceTokenRepository tokens) => _tokens = tokens;

    public async Task<Unit> Handle(RegisterDeviceTokenCommand request, CancellationToken cancellationToken)
    {
        var token = request.Token?.Trim() ?? string.Empty;
        var deviceId = request.DeviceId?.Trim() ?? string.Empty;
        if (request.UserId == Guid.Empty)
            throw new BusinessRuleException("DeviceToken.User", "A device token belongs to a signed-in user.");
        if (token.Length == 0 || token.Length > MaxTokenLength)
            throw new BusinessRuleException("DeviceToken.Token", $"The device token is 1-{MaxTokenLength} characters.");
        if (deviceId.Length == 0 || deviceId.Length > MaxDeviceIdLength)
            throw new BusinessRuleException("DeviceToken.DeviceId", $"The device id is 1-{MaxDeviceIdLength} characters.");
        var platform = Platforms.Contains(request.Platform?.Trim().ToLowerInvariant() ?? "") ? request.Platform!.Trim().ToLowerInvariant() : "android";

        var byDevice = await _tokens.GetByDeviceAsync(request.UserId, deviceId, cancellationToken);
        var byToken = await _tokens.GetByTokenAsync(token, cancellationToken);

        // The token is unique per tenant: when another row holds it (an older install of this phone, or the phone passed to another account), that row goes.
        if (byToken != null && byDevice != null && byToken.Id != byDevice.Id)
        {
            await _tokens.RemoveAsync(byToken, cancellationToken);
            byToken = null;
        }

        if (byDevice != null)
        {
            byDevice.Replace(request.UserId, token, deviceId, platform);
            await _tokens.UpdateAsync(byDevice, cancellationToken);
        }
        else if (byToken != null)
        {
            byToken.Replace(request.UserId, token, deviceId, platform);
            await _tokens.UpdateAsync(byToken, cancellationToken);
        }
        else
        {
            await _tokens.AddAsync(DeviceToken.Create(request.UserId, token, deviceId, platform), cancellationToken);
        }
        return Unit.Value;
    }
}

public class UnregisterDeviceTokenCommandHandler : IRequestHandler<UnregisterDeviceTokenCommand, bool>
{
    private readonly IDeviceTokenRepository _tokens;

    public UnregisterDeviceTokenCommandHandler(IDeviceTokenRepository tokens) => _tokens = tokens;

    public async Task<bool> Handle(UnregisterDeviceTokenCommand request, CancellationToken cancellationToken)
    {
        var existing = await _tokens.GetByDeviceAsync(request.UserId, request.DeviceId.Trim(), cancellationToken);
        if (existing == null) return false;
        await _tokens.RemoveAsync(existing, cancellationToken);
        return true;
    }
}
