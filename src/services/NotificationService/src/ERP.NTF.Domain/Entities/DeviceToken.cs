using ERP.Shared.Core.Domain;

namespace ERP.NTF.Domain.Entities;

/// <summary>
/// A phone that may receive push messages for a user: the token the device got from the push service (FCM), kept per user and device so a
/// user with several phones is reached on each. One row per (tenant, user, device); a token belongs to one user at a time (it moves to the
/// account that signed in last). Removed when the user signs out, or when the push service says the token is dead. The command handler
/// refuses an empty token or device id before it gets here.
/// </summary>
public class DeviceToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    /// <summary>Stable id of the install on the phone (the app generates it once); the same phone registering again replaces its token.</summary>
    public string DeviceId { get; private set; } = string.Empty;
    /// <summary>android or ios.</summary>
    public string Platform { get; private set; } = "android";
    public DateTime LastSeenAt { get; private set; }

    private DeviceToken() { }

    public static DeviceToken Create(Guid userId, string token, string deviceId, string platform)
    {
        var created = new DeviceToken { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        created.Replace(userId, token, deviceId, platform);
        return created;
    }

    /// <summary>The same row now belongs to this user / device with this token (a token refresh, or a phone passed to another account).</summary>
    public void Replace(Guid userId, string token, string deviceId, string platform)
    {
        UserId = userId;
        Token = token.Trim();
        DeviceId = deviceId.Trim();
        Platform = string.IsNullOrWhiteSpace(platform) ? "android" : platform.Trim().ToLowerInvariant();
        LastSeenAt = DateTime.UtcNow;
    }
}
