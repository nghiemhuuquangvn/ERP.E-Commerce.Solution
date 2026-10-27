using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities;

namespace ERP.SHP.Application.Services;

/// <summary>A carrier to try and why it is tried (kept on the shipment as the selection note).</summary>
public record CarrierCandidate(string CarrierCode, string Reason);

/// <summary>
/// Which carriers an order is offered to, in order. First the shop's rules (ascending priority, active, matching the order, naming a carrier the
/// shop can use); then every other usable carrier, the cheapest quote first, carriers that cannot quote last. The first one that books wins;
/// a refusal moves on to the next. Pure logic — the calls to the carriers are made by <see cref="CarrierBookingService"/>.
/// </summary>
public static class CarrierSelector
{
    public static List<CarrierCandidate> ByRules(IEnumerable<CarrierSelectionRule> rules, ShipmentFacts facts, IReadOnlySet<string> usable)
    {
        var list = new List<CarrierCandidate>();
        foreach (var rule in rules.Where(r => r.IsActive).OrderBy(r => r.Priority).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!usable.Contains(rule.CarrierCode) || !rule.Matches(facts) || list.Any(c => c.CarrierCode == rule.CarrierCode)) continue;
            list.Add(new CarrierCandidate(rule.CarrierCode, $"rule '{rule.Name}'"));
        }
        return list;
    }

    public static List<CarrierCandidate> ByPrice(IEnumerable<CarrierQuote> quotes, IReadOnlySet<string> alreadyTried)
    {
        var open = quotes.Where(q => !alreadyTried.Contains(q.CarrierCode)).ToList();
        var priced = open.Where(q => q.Success).OrderBy(q => q.Fee).ThenBy(q => q.CarrierCode, StringComparer.Ordinal).ToList();
        var list = priced.Select((q, i) => new CarrierCandidate(q.CarrierCode,
            priced.Count == 1 ? $"only quote ({q.Fee:N0} VND)" : $"quote {q.Fee:N0} VND, {(i == 0 ? "the cheapest" : $"number {i + 1}")} of {priced.Count}")).ToList();
        list.AddRange(open.Where(q => !q.Success).OrderBy(q => q.CarrierCode, StringComparer.Ordinal)
            .Select(q => new CarrierCandidate(q.CarrierCode, "could not quote, tried last")));
        return list;
    }
}
