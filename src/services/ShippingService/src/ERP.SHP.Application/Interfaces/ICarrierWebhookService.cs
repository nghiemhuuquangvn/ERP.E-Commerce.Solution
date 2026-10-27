namespace ERP.SHP.Application.Interfaces;

public enum CarrierWebhookOutcome
{
    /// <summary>Verified and applied.</summary>
    Applied,
    /// <summary>Verified, but already applied before (a retry): nothing changed.</summary>
    Duplicate,
    UnknownCarrier,
    UnknownShipment,
    /// <summary>The shop's account cannot verify it (carrier switched off, no secret).</summary>
    NotVerifiable,
    /// <summary>The secret did not match or the body is not a notification of that carrier.</summary>
    Rejected,
}

public record CarrierWebhookResult(CarrierWebhookOutcome Outcome, string? Message, CarrierWebhookReply Reply);

/// <summary>The one path every carrier notification takes: find the shop from the tracking code, verify with THAT shop's secret, apply once.</summary>
public interface ICarrierWebhookService
{
    Task<CarrierWebhookResult> ProcessAsync(string carrier, CarrierWebhookRequest request, CancellationToken ct = default);
}
