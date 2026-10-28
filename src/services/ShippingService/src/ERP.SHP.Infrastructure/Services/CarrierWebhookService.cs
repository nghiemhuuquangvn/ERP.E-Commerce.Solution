using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Services;
using ERP.SHP.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ERP.SHP.Infrastructure.Services;

/// <summary>
/// Carrier notifications (anonymous routes, authenticated by the shop's shared secret). Mirrors Payment's GatewayNotificationService:
/// peek the tracking code → find the shop that owns that shipment → verify with THAT shop's account → the verified code must be the peeked one →
/// apply once (idempotent through <see cref="CarrierWebhookLog"/>). Never add a second path.
/// </summary>
public class CarrierWebhookService : ICarrierWebhookService
{
    private readonly IShippingUnitOfWork _uow;
    private readonly ICarrierProviderResolver _carriers;
    private readonly CarrierTrackingService _tracking;
    private readonly ILogger<CarrierWebhookService> _logger;

    public CarrierWebhookService(IShippingUnitOfWork uow, ICarrierProviderResolver carriers, CarrierTrackingService tracking, ILogger<CarrierWebhookService> logger)
    {
        _uow = uow;
        _carriers = carriers;
        _tracking = tracking;
        _logger = logger;
    }

    public async Task<CarrierWebhookResult> ProcessAsync(string carrier, CarrierWebhookRequest request, CancellationToken ct = default)
    {
        var code = CarrierCodes.Normalize(carrier);
        var provider = code == null ? null : _carriers.Provider(code);
        if (code == null || provider == null || !provider.Implemented)
            return new CarrierWebhookResult(CarrierWebhookOutcome.UnknownCarrier, $"'{carrier}' is not a carrier this platform integrates", new CarrierWebhookReply(404, new { message = "unknown carrier" }));

        // The tracking code is read before it is trusted, only to find whose secret verifies the message; the verified code must be the same.
        var peeked = provider.PeekTrackingCode(request);
        if (string.IsNullOrWhiteSpace(peeked))
            return Reject(provider, CarrierWebhookOutcome.Rejected, "no tracking code");
        if (await _uow.Shipments.FindTenantByTrackingNumberAsync(peeked, ct) is not { } tenantId)
            return Reject(provider, CarrierWebhookOutcome.UnknownShipment, "unknown tracking code");

        using var tenantScope = TenantScope.Begin(tenantId);

        ResolvedCarrier resolved;
        try
        {
            resolved = await _carriers.ResolveAsync(code, ct);
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogWarning("[{Carrier}] Notification for {Tracking} cannot be verified: {Reason}", code, peeked, ex.Message);
            return Reject(provider, CarrierWebhookOutcome.NotVerifiable, ex.Message);
        }

        var evt = resolved.Provider.VerifyWebhook(resolved.Credentials, request);
        if (!evt.Verified || !string.Equals(evt.TrackingCode, peeked, StringComparison.Ordinal))
        {
            _logger.LogWarning("[{Carrier}] Rejected notification for {Tracking}: {Error}", code, peeked, evt.Error ?? "tracking code mismatch");
            return Reject(provider, CarrierWebhookOutcome.Rejected, "verification failed");
        }

        if (await _uow.Shipments.WebhookSeenAsync(code, peeked, evt.CarrierStatus, evt.ActionTime, ct))
            return new CarrierWebhookResult(CarrierWebhookOutcome.Duplicate, null, provider.WebhookReply(true, "already applied"));

        var shipment = await _uow.Shipments.GetTrackedByTrackingNumberAsync(peeked, ct);
        if (shipment != null)
            await _tracking.ApplyAsync(shipment, evt.Status, evt.CarrierStatus, evt.Reason, evt.CodAmount, ct);
        else if (await _uow.ReturnShipments.GetTrackedByTrackingNumberAsync(peeked, ct) is { } returned)
            await _tracking.ApplyReturnAsync(returned, evt.Status, ct);   // the reverse parcel of a return (P1-11d)
        else
            return Reject(provider, CarrierWebhookOutcome.UnknownShipment, "unknown tracking code");

        await _uow.Shipments.AddWebhookLogAsync(CarrierWebhookLog.Create(tenantId, code, peeked, evt.CarrierStatus, evt.ActionTime), ct);
        await _uow.SaveChangesAsync(ct);

        return new CarrierWebhookResult(CarrierWebhookOutcome.Applied, null, provider.WebhookReply(true, "ok"));
    }

    private static CarrierWebhookResult Reject(ICarrierProvider provider, CarrierWebhookOutcome outcome, string message) =>
        new(outcome, message, provider.WebhookReply(false, message));
}
