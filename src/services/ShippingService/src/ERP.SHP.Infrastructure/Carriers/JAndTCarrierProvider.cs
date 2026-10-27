using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Enums;

namespace ERP.SHP.Infrastructure.Carriers;

/// <summary>
/// J&amp;T Express (Vietnam): <b>placeholder, not an integration</b>. The official API documentation could not be read (the public search finds only the Indonesian and
/// Malaysian partner APIs, whose signing schemes differ from each other), and a guessed signature scheme would book nothing and hide the reason. The carrier code is
/// reserved and accepted in settings and rules, the provider says plainly it is unavailable, and it never takes part in selection. Replace this class when
/// the partner documentation and a sandbox account are in hand (DEFERRED P1-11b).
/// </summary>
public class JAndTCarrierProvider : ICarrierProvider
{
    private const string Reason = "J&T Express is not integrated yet: the partner API documentation is not available to this project";

    public string Code => CarrierCodes.JAndT;
    public bool Implemented => false;

    public Task<CarrierQuote> QuoteAsync(CarrierCredentials credentials, CarrierQuoteRequest request, CancellationToken ct = default) =>
        Task.FromResult(new CarrierQuote(Code, false, 0, null, null, Reason));

    public Task<CarrierBooking> BookAsync(CarrierCredentials credentials, CarrierBookRequest request, CancellationToken ct = default) =>
        Task.FromResult(new CarrierBooking(false, null, null, null, null, Reason));

    public Task<CarrierCancelResult> CancelAsync(CarrierCredentials credentials, string trackingCode, string reason, CancellationToken ct = default) =>
        Task.FromResult(new CarrierCancelResult(false, Reason));

    public Task<CarrierStatusResult> GetStatusAsync(CarrierCredentials credentials, string trackingCode, CancellationToken ct = default) =>
        Task.FromResult(new CarrierStatusResult(false, null, CarrierShipmentStatus.Accepted, null, null, Reason));

    public string? PeekTrackingCode(CarrierWebhookRequest request) => null;

    public CarrierWebhookEvent VerifyWebhook(CarrierCredentials credentials, CarrierWebhookRequest request) =>
        new(false, Reason, null, string.Empty, CarrierShipmentStatus.Accepted, DateTime.UtcNow, null, null);

    public CarrierWebhookReply WebhookReply(bool accepted, string message) => new(accepted ? 200 : 400, new { message });
}
