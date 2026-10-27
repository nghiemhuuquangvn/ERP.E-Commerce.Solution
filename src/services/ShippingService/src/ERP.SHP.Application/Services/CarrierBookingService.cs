using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ERP.SHP.Application.Services;

/// <summary>No carrier accepted the parcel (or none is set up). The message lists every carrier tried and its reason.</summary>
public class CarrierBookingException : Exception
{
    /// <summary>True when every refusal was a carrier that could not be reached: worth trying again later, not a reason to cancel the order.</summary>
    public bool IsTransient { get; }

    public CarrierBookingException(string message, bool isTransient = false) : base(message) => IsTransient = isTransient;
}

/// <summary>Everything the booking needs beyond the shipment itself.</summary>
public record BookingOrder(string OrderReference, string? Note);

/// <summary>
/// Books a parcel with a carrier: finds the carriers the shop can use, orders them by rule then by price (<see cref="CarrierSelector"/>), tries them
/// until one accepts, and writes the answer (codes, fee, ETA, why this carrier) on the shipment. A refusal is never swallowed: when every carrier
/// refuses, <see cref="CarrierBookingException"/> carries all the reasons and the order saga compensates.
/// </summary>
public class CarrierBookingService
{
    private readonly IShippingUnitOfWork _uow;
    private readonly ICarrierProviderResolver _carriers;
    private readonly ILogger<CarrierBookingService> _logger;

    public CarrierBookingService(IShippingUnitOfWork uow, ICarrierProviderResolver carriers, ILogger<CarrierBookingService> logger)
    {
        _uow = uow;
        _carriers = carriers;
        _logger = logger;
    }

    /// <summary>The fee every usable carrier asks for this parcel (for the admin's quote panel and for the selection by price).</summary>
    public async Task<List<CarrierQuote>> QuoteAllAsync(CarrierQuoteRequest request, CancellationToken ct = default)
    {
        var usable = await _carriers.ListEnabledAsync(ct);
        var quotes = await Task.WhenAll(usable.Select(c => c.Provider.QuoteAsync(c.Credentials, request, ct)));
        return quotes.ToList();
    }

    /// <summary>What the selection would do with this parcel: the rule that matches (if any) and every carrier's quote. Books nothing.</summary>
    public async Task<ERP.SHP.Application.DTOs.CarrierChoicePreviewDto> PreviewAsync(CarrierQuoteRequest request, CancellationToken ct = default)
    {
        var usable = await _carriers.ListEnabledAsync(ct);
        var quotes = await Task.WhenAll(usable.Select(c => c.Provider.QuoteAsync(c.Credentials, request, ct)));
        var codes = usable.Select(c => c.Credentials.CarrierCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var facts = new ShipmentFacts(request.To.Province, request.WeightGrams, request.DeclaredValue, request.CodAmount > 0m);
        var rule = (await _uow.Config.GetActiveRulesAsync(ct)).OrderBy(r => r.Priority).FirstOrDefault(r => codes.Contains(r.CarrierCode) && r.Matches(facts));
        return new ERP.SHP.Application.DTOs.CarrierChoicePreviewDto(rule?.CarrierCode, rule?.Name,
            quotes.Select(q => new ERP.SHP.Application.DTOs.CarrierQuotePreviewDto(q.CarrierCode, NameOf(q.CarrierCode), q.Success, q.Fee, q.Error)).ToList());
    }

    public async Task<CarrierSender> SenderAsync(CancellationToken ct = default)
    {
        var settings = await _uow.Config.GetTenantSettingsAsync(ct);
        if (settings is null || !settings.HasSender)
            throw new CarrierBookingException("the shop's pickup address is not set (Settings → Shipping → pickup address)");
        return new CarrierSender(
            new CarrierAddress(settings.SenderName!, settings.SenderPhone!, settings.SenderAddress!, settings.SenderWard, settings.SenderDistrict!, settings.SenderProvince!),
            settings.DefaultItemWeightGrams);
    }

    /// <summary>
    /// Books <paramref name="shipment"/>. <paramref name="onlyCarrierCode"/> (a person chose the carrier) restricts the attempt to that carrier.
    /// Returns once the shipment carries the carrier's answer; throws <see cref="CarrierBookingException"/> when nobody accepted it.
    /// </summary>
    public async Task BookAsync(Shipment shipment, CarrierAddress sender, IReadOnlyList<CarrierParcelItem> items, BookingOrder order, string? onlyCarrierCode, CancellationToken ct = default)
    {
        var usable = await _carriers.ListEnabledAsync(ct);
        var byCode = usable.ToDictionary(c => c.Credentials.CarrierCode, StringComparer.OrdinalIgnoreCase);
        if (onlyCarrierCode != null)
        {
            // Throws the business-rule reason (off, unavailable, no account) when the chosen carrier cannot be used.
            var chosen = await _carriers.ResolveAsync(onlyCarrierCode, ct);
            byCode = new Dictionary<string, ResolvedCarrier>(StringComparer.OrdinalIgnoreCase) { [chosen.Credentials.CarrierCode] = chosen };
        }
        if (byCode.Count == 0)
            throw new CarrierBookingException("no carrier is set up for this shop (Settings → Shipping → carrier accounts)");

        var weight = Math.Max(1, (int)Math.Round(shipment.TotalWeight * 1000m));
        var to = new CarrierAddress(shipment.RecipientName, shipment.RecipientPhone ?? string.Empty, shipment.AddressStreet, shipment.AddressWard,
            shipment.AddressDistrict, shipment.AddressProvince);
        var request = new CarrierBookRequest(order.OrderReference, sender, to, weight, shipment.DeclaredValue, shipment.CodAmount, order.Note, items);

        var usableCodes = byCode.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var facts = new ShipmentFacts(shipment.AddressProvince, weight, shipment.DeclaredValue, shipment.CodAmount > 0m);
        var errors = new List<string>();
        var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. the shop's rules; a person's own choice is the only candidate
        var first = onlyCarrierCode != null
            ? new List<CarrierCandidate> { new(byCode.Keys.Single(), "chosen by a person") }
            : CarrierSelector.ByRules(await _uow.Config.GetActiveRulesAsync(ct), facts, usableCodes);
        if (await TryAsync(first) ) return;

        // 2. every other carrier, cheapest quote first
        var rest = byCode.Where(p => !tried.Contains(p.Key)).Select(p => p.Value).ToList();
        if (rest.Count > 0)
        {
            var quoteRequest = new CarrierQuoteRequest(sender, to, weight, shipment.DeclaredValue, shipment.CodAmount);
            var quotes = await Task.WhenAll(rest.Select(c => c.Provider.QuoteAsync(c.Credentials, quoteRequest, ct)));
            if (await TryAsync(CarrierSelector.ByPrice(quotes, tried))) return;
        }

        var reason = errors.Count == 0 ? "no carrier could be tried" : string.Join("; ", errors);
        shipment.RecordBookingFailure(reason);
        throw new CarrierBookingException(reason, isTransient: errors.Count > 0 && errors.All(e => e.Contains("not reachable", StringComparison.OrdinalIgnoreCase)));

        async Task<bool> TryAsync(IEnumerable<CarrierCandidate> candidates)
        {
            foreach (var candidate in candidates)
            {
                if (!tried.Add(candidate.CarrierCode)) continue;
                var carrier = byCode[candidate.CarrierCode];
                CarrierBooking booking;
                try { booking = await carrier.Provider.BookAsync(carrier.Credentials, request, ct); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Carrier {Carrier} failed unexpectedly while booking {Order}", candidate.CarrierCode, order.OrderReference);
                    booking = new CarrierBooking(false, null, null, null, null, ex.Message);
                }

                if (!booking.Success || string.IsNullOrEmpty(booking.TrackingCode))
                {
                    errors.Add($"{candidate.CarrierCode}: {booking.Error ?? "no tracking code"}");
                    continue;
                }

                var row = await EnsureCarrierRowAsync(candidate.CarrierCode, ct);
                shipment.RecordBooking(row.Id, row.Name, booking.TrackingCode!, booking.CarrierOrderCode, booking.Fee, booking.EstimatedDelivery,
                    candidate.Reason + (errors.Count > 0 ? $" (after: {string.Join("; ", errors)})" : string.Empty));
                return true;
            }
            return false;
        }
    }

    /// <summary>The shop's <see cref="Carrier"/> row for a carrier code (the shipment points at it); created on first use.</summary>
    public async Task<Carrier> EnsureCarrierRowAsync(string code, CancellationToken ct = default)
    {
        var existing = await _uow.Carriers.GetByCodeAsync(code, ct);
        if (existing != null) return existing;

        var created = Carrier.Create(NameOf(code), code, 0m);
        await _uow.Carriers.AddAsync(created, ct);
        return created;
    }

    public static string NameOf(string code) => code switch
    {
        CarrierCodes.Ghtk => "Giao Hàng Tiết Kiệm (GHTK)",
        CarrierCodes.ViettelPost => "Viettel Post",
        CarrierCodes.Ghn => "Giao Hàng Nhanh (GHN)",
        CarrierCodes.JAndT => "J&T Express",
        _ => code
    };
}

public record CarrierSender(CarrierAddress Address, int DefaultItemWeightGrams);
