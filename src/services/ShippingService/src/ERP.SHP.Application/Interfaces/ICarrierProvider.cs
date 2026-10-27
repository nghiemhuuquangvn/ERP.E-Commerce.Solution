using ERP.SHP.Domain.Enums;

namespace ERP.SHP.Application.Interfaces;

/// <summary>
/// A tenant's decrypted account at one carrier, passed into every provider call (providers keep no tenant state).
/// Meaning per carrier: see <see cref="ERP.SHP.Domain.Entities.Settings.TenantCarrierCredential"/>.
/// <see cref="WebhookSecret"/> is the shared secret the shop put in the callback address it gave the carrier (<c>?hash=…</c>).
/// </summary>
public record CarrierCredentials(string CarrierCode, string? PublicId, string? Username, string? Secret1, string? WebhookSecret, bool Sandbox);

public record CarrierAddress(string Name, string Phone, string Street, string? Ward, string District, string Province);

public record CarrierParcelItem(string Name, int Quantity, decimal UnitPrice, int WeightGrams);

public record CarrierQuoteRequest(CarrierAddress From, CarrierAddress To, int WeightGrams, decimal DeclaredValue, decimal CodAmount);

/// <param name="Success">false = this carrier cannot price the parcel (address not served, no data, error): it is skipped, not "free".</param>
public record CarrierQuote(string CarrierCode, bool Success, decimal Fee, string? Service, string? Eta, string? Error);

public record CarrierBookRequest(
    string OrderReference, CarrierAddress From, CarrierAddress To, int WeightGrams, decimal DeclaredValue, decimal CodAmount,
    string? Note, IReadOnlyList<CarrierParcelItem> Items);

public record CarrierBooking(bool Success, string? TrackingCode, string? CarrierOrderCode, decimal? Fee, DateTime? EstimatedDelivery, string? Error);

public record CarrierCancelResult(bool Success, string? Error);

public record CarrierStatusResult(bool Success, string? CarrierStatus, CarrierShipmentStatus Status, string? Description, DateTime? Time, string? Error);

/// <summary>An HTTP request a carrier sent to the shop's anonymous webhook address.</summary>
public record CarrierWebhookRequest(string Method, IReadOnlyDictionary<string, string> Query, string Body);

/// <summary>A verified carrier notification. <see cref="CodAmount"/> is the cash the carrier says it collected (when it says).</summary>
public record CarrierWebhookEvent(
    bool Verified, string? Error, string? TrackingCode, string CarrierStatus, CarrierShipmentStatus Status, DateTime ActionTime, string? Reason, decimal? CodAmount);

/// <summary>What the carrier expects back from a webhook call.</summary>
public record CarrierWebhookReply(int StatusCode, object? Body);

/// <summary>
/// One carrier's API (GHTK, Viettel Post, GHN, J&amp;T). Stateless: the shop's credentials come in with every call and the carrier's host
/// comes from configuration (sandbox or production) — never from tenant input. Nothing throws for a carrier-side refusal: the result says why.
/// </summary>
public interface ICarrierProvider
{
    /// <summary>One of <see cref="ERP.SHP.Domain.Entities.CarrierCodes"/>.</summary>
    string Code { get; }

    /// <summary>false = the integration is a placeholder (no verified API): the shop cannot enable it and it never takes part in selection.</summary>
    bool Implemented => true;

    Task<CarrierQuote> QuoteAsync(CarrierCredentials credentials, CarrierQuoteRequest request, CancellationToken ct = default);

    Task<CarrierBooking> BookAsync(CarrierCredentials credentials, CarrierBookRequest request, CancellationToken ct = default);

    Task<CarrierCancelResult> CancelAsync(CarrierCredentials credentials, string trackingCode, string reason, CancellationToken ct = default);

    Task<CarrierStatusResult> GetStatusAsync(CarrierCredentials credentials, string trackingCode, CancellationToken ct = default);

    /// <summary>The tracking code named in a webhook, read BEFORE verification only to find the shop whose account verifies it.</summary>
    string? PeekTrackingCode(CarrierWebhookRequest request);

    /// <summary>Checks the request really comes from the carrier (shared secret) and translates it.</summary>
    CarrierWebhookEvent VerifyWebhook(CarrierCredentials credentials, CarrierWebhookRequest request);

    CarrierWebhookReply WebhookReply(bool accepted, string message);
}

/// <summary>The provider and the CURRENT shop's account for one carrier.</summary>
public record ResolvedCarrier(ICarrierProvider Provider, CarrierCredentials Credentials);

public interface ICarrierProviderResolver
{
    /// <summary>Throws a business-rule error when the carrier is off on the platform or for the shop, or has no complete account.</summary>
    Task<ResolvedCarrier> ResolveAsync(string carrierCode, CancellationToken ct = default);

    /// <summary>Every carrier the shop can use now (platform on, shop on, account complete).</summary>
    Task<IReadOnlyList<ResolvedCarrier>> ListEnabledAsync(CancellationToken ct = default);

    ICarrierProvider? Provider(string carrierCode);
}
