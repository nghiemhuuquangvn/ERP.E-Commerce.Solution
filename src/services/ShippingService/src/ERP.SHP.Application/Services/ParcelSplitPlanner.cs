using ERP.Shared.Infrastructure.Exceptions;
using ERP.SHP.Domain.Entities;

namespace ERP.SHP.Application.Services;

/// <summary>What a person asks for one new parcel: the quantities of products it carries and, optionally, the cash the carrier collects on it.</summary>
public record ParcelSplitPart(IReadOnlyList<ParcelSplitLine> Lines, decimal? CodAmount);

public record ParcelSplitLine(Guid ProductId, int Quantity);

/// <summary>One planned parcel: its items (with the weights of the parcel being split), the cash it collects and the value declared on it.</summary>
public record PlannedParcel(int ParcelNo, List<(Guid ProductId, string Name, int Quantity, decimal Weight)> Items, decimal CodAmount, decimal DeclaredValue);

/// <summary>
/// Works out how an order's parcel splits into several (ROADMAP P1-11d). Pure: the rules are tested without a carrier. The parts must carry exactly the quantities of
/// the parcel being split (nothing lost, nothing invented); the cash on delivery must add up to the parcel's, so the order's cash is collected once in total.
/// </summary>
public static class ParcelSplitPlanner
{
    public const int MaxParcels = 10;

    public static List<PlannedParcel> Plan(Shipment shipment, IReadOnlyList<ParcelSplitPart> parts)
    {
        if (parts.Count < 2) throw new BusinessRuleException("Shipping.Parcels", "a split needs at least two parcels");
        if (parts.Count > MaxParcels) throw new BusinessRuleException("Shipping.Parcels", $"an order is split into at most {MaxParcels} parcels");

        var have = shipment.Items.GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        var given = new Dictionary<Guid, int>();
        foreach (var part in parts)
        {
            if (part.Lines.Count == 0 || part.Lines.All(l => l.Quantity <= 0))
                throw new BusinessRuleException("Shipping.Parcels", "every parcel must carry at least one item");
            foreach (var line in part.Lines)
            {
                if (line.Quantity < 0) throw new BusinessRuleException("Shipping.Parcels", "a quantity cannot be negative");
                if (!have.ContainsKey(line.ProductId)) throw new BusinessRuleException("Shipping.Parcels", $"product {line.ProductId} is not in this parcel");
                given[line.ProductId] = given.GetValueOrDefault(line.ProductId) + line.Quantity;
            }
        }
        foreach (var (productId, qty) in have)
        {
            var assigned = given.GetValueOrDefault(productId);
            if (assigned != qty)
                throw new BusinessRuleException("Shipping.Parcels", $"the parcels carry {assigned} of product {productId} but the parcel holds {qty}: every item must go to exactly one parcel");
        }

        var explicitCod = parts.Count(p => p.CodAmount.HasValue);
        if (explicitCod != 0 && explicitCod != parts.Count)
            throw new BusinessRuleException("Shipping.Parcels", "give the cash on delivery of every parcel or of none");
        if (explicitCod == parts.Count)
        {
            if (parts.Any(p => p.CodAmount < 0)) throw new BusinessRuleException("Shipping.Parcels", "cash on delivery cannot be negative");
            if (parts.Sum(p => p.CodAmount!.Value) != shipment.CodAmount)
                throw new BusinessRuleException("Shipping.Parcels", $"the cash on delivery of the parcels ({parts.Sum(p => p.CodAmount!.Value):N0}) must equal the parcel's ({shipment.CodAmount:N0})");
        }

        var totalUnits = parts.Sum(p => p.Lines.Sum(l => l.Quantity));
        var result = new List<PlannedParcel>();
        decimal codLeft = shipment.CodAmount, declaredLeft = shipment.DeclaredValue;
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            var units = part.Lines.Sum(l => l.Quantity);
            var last = i == parts.Count - 1;

            var items = new List<(Guid, string, int, decimal)>();
            foreach (var line in part.Lines.Where(l => l.Quantity > 0))
            {
                var source = shipment.Items.First(s => s.ProductId == line.ProductId);
                items.Add((line.ProductId, source.ProductName, line.Quantity, source.Weight));
            }

            // Cash and declared value follow the units unless a person typed the cash; the last parcel takes what is left, so the totals are exact.
            var cod = part.CodAmount ?? (last ? codLeft : Math.Round(shipment.CodAmount * units / totalUnits, 0));
            var declared = last ? declaredLeft : Math.Round(shipment.DeclaredValue * units / totalUnits, 0);
            codLeft -= cod;
            declaredLeft -= declared;
            result.Add(new PlannedParcel(i + 1, items, cod, declared));
        }

        if (result.Any(r => r.CodAmount < 0 || r.DeclaredValue < 0))
            throw new BusinessRuleException("Shipping.Parcels", "the cash on delivery cannot be shared between these parcels, type it for each parcel");
        return result;
    }
}
