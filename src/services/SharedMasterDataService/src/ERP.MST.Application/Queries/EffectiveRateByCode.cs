using ERP.MST.Application.Interfaces;
using MediatR;

namespace ERP.MST.Application.Queries;

/// <summary>The rate that converts one unit of <paramref name="From"/> into <paramref name="To"/> on a day (service-to-service read for Accounting, ROADMAP P1-4).</summary>
public record GetEffectiveRateByCodeQuery(string From, string To, DateTime AsOfDate) : IRequest<EffectiveRateByCodeDto?>;

public record EffectiveRateByCodeDto(string From, string To, decimal Rate, DateTime EffectiveDate, bool Inverted);

public class GetEffectiveRateByCodeQueryHandler : IRequestHandler<GetEffectiveRateByCodeQuery, EffectiveRateByCodeDto?>
{
    private readonly ICurrencyRepository _currencies;
    private readonly IExchangeRateRepository _rates;

    public GetEffectiveRateByCodeQueryHandler(ICurrencyRepository currencies, IExchangeRateRepository rates)
    {
        _currencies = currencies;
        _rates = rates;
    }

    public async Task<EffectiveRateByCodeDto?> Handle(GetEffectiveRateByCodeQuery request, CancellationToken cancellationToken)
    {
        var from = request.From.Trim().ToUpperInvariant();
        var to = request.To.Trim().ToUpperInvariant();
        if (from == to)
            return new EffectiveRateByCodeDto(from, to, 1m, request.AsOfDate.Date, false);

        var fromCurrency = await _currencies.GetByCodeAsync(from, cancellationToken);
        var toCurrency = await _currencies.GetByCodeAsync(to, cancellationToken);
        if (fromCurrency is null || toCurrency is null)
            return null;

        var direct = await _rates.GetEffectiveRateAsync(fromCurrency.Id, toCurrency.Id, request.AsOfDate, cancellationToken);
        var inverse = await _rates.GetEffectiveRateAsync(toCurrency.Id, fromCurrency.Id, request.AsOfDate, cancellationToken);
        // the newest of the two directions wins (a rate is often entered one way only)
        if (direct is not null && (inverse is null || direct.EffectiveDate >= inverse.EffectiveDate) && direct.Rate > 0m)
            return new EffectiveRateByCodeDto(from, to, direct.Rate, direct.EffectiveDate, false);
        if (inverse is not null && inverse.Rate > 0m)
            return new EffectiveRateByCodeDto(from, to, Math.Round(1m / inverse.Rate, 6), inverse.EffectiveDate, true);
        return null;
    }
}
