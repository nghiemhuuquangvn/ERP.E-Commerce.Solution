using ERP.NTF.Application.Interfaces;
using ERP.NTF.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ERP.NTF.Application.Services;

/// <summary>
/// Delivers a Push notification to every phone that may get it: the one device token on the row, or every device the recipient user registered.
/// A device the push service no longer knows is forgotten. One broken device does not stop the others; the delivery counts as failed only when
/// no device could be reached (so the dispatch job retries it).
/// </summary>
public class PushDelivery
{
    private readonly IDeviceTokenRepository _tokens;
    private readonly IPushSender _sender;
    private readonly ILogger<PushDelivery> _logger;

    public PushDelivery(IDeviceTokenRepository tokens, IPushSender sender, ILogger<PushDelivery> logger)
    {
        _tokens = tokens;
        _sender = sender;
        _logger = logger;
    }

    /// <summary>How many devices got the message (0 when the user has none registered).</summary>
    public async Task<int> DeliverAsync(Notification notification, CancellationToken ct)
    {
        var targets = new List<(string Token, DeviceToken? Row)>();
        if (!string.IsNullOrEmpty(notification.RecipientDeviceToken))
            targets.Add((notification.RecipientDeviceToken, null));
        else if (Guid.TryParse(notification.RecipientId, out var userId))
            targets.AddRange((await _tokens.GetByUserAsync(userId, ct)).Select(d => (d.Token, (DeviceToken?)d)));

        var data = LinkData(notification);
        var delivered = 0;
        Exception? lastError = null;
        foreach (var (token, row) in targets)
        {
            try
            {
                await _sender.SendAsync(token, notification.Subject, notification.Body, ct, data);
                delivered++;
            }
            catch (InvalidPushTokenException)
            {
                if (row != null)
                {
                    _logger.LogInformation("Push token of device {DeviceId} is no longer valid; forgotten", row.DeviceId);
                    await _tokens.RemoveAsync(row, ct);
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.LogWarning(ex, "Push to one device of {Recipient} failed", notification.RecipientId);
            }
        }

        if (delivered == 0 && lastError != null) throw lastError;
        return delivered;
    }

    private static IReadOnlyDictionary<string, string>? LinkData(Notification n)
    {
        if (string.IsNullOrEmpty(n.EntityType) || string.IsNullOrEmpty(n.EntityId)) return null;
        var data = new Dictionary<string, string> { ["entityType"] = n.EntityType, ["entityId"] = n.EntityId };
        if (!string.IsNullOrEmpty(n.RouteKey)) data["routeKey"] = n.RouteKey;
        return data;
    }
}
