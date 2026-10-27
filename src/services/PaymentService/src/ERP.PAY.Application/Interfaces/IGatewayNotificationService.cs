namespace ERP.PAY.Application.Interfaces;

public enum NotificationOutcome
{
    /// <summary>Verified; the payment was captured (or had been already).</summary>
    Paid,
    /// <summary>Verified; the gateway says the payment did not complete and the payment was failed.</summary>
    Failed,
    InvalidSignature,
    UnknownReference,
    /// <summary>Signature fine but the amount is not the payment's: nothing captured, the payment is flagged for review.</summary>
    AmountMismatch,
    /// <summary>The gateway is not usable for this tenant any more (switched off, keys removed) so the notification cannot be verified.</summary>
    NotVerifiable
}

/// <param name="Reply">What to answer the gateway when this was its server-to-server notification.</param>
public record GatewayNotificationResult(NotificationOutcome Outcome, Guid? OrderId, string? Message, GatewayIpnReply Reply)
{
    public bool Accepted => Outcome is NotificationOutcome.Paid or NotificationOutcome.Failed;
}

/// <summary>
/// The one path for everything a gateway sends back (the browser return and the server notification): find the payment's tenant from the
/// reference, verify the signature with THAT tenant's keys, check the amount, then apply it (idempotent).
/// </summary>
public interface IGatewayNotificationService
{
    Task<GatewayNotificationResult> ProcessAsync(string provider, GatewayCallback callback, CancellationToken ct = default);
}
