using ERP.MST.Application.Interfaces;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.MST.Application.Internal;

// Service-to-service reads of units of measure (ManufacturingService, BA spec 01 §8 IUomConversionService). Reference data only.

public record UnitLookupDto(Guid Id, string Code, string Name, int DecimalPlaces, Guid? BaseUnitId, decimal ConversionFactor, int UnitType);

public record LookupUnitsOfMeasureQuery(IReadOnlyCollection<Guid> Ids) : IRequest<List<UnitLookupDto>>;

public class LookupUnitsOfMeasureQueryHandler(IUnitOfMeasureRepository units) : IRequestHandler<LookupUnitsOfMeasureQuery, List<UnitLookupDto>>
{
    public async Task<List<UnitLookupDto>> Handle(LookupUnitsOfMeasureQuery request, CancellationToken ct)
    {
        var ids = request.Ids.Distinct().ToList();
        if (ids.Count == 0) return new();
        var found = await units.GetByIdsAsync(ids, ct);
        return found.Select(u => new UnitLookupDto(u.Id, u.Code, u.Name, u.DecimalPlaces, u.BaseUnitId, u.ConversionFactor, u.UnitType)).ToList();
    }
}

public record ConvertUnitQuery(Guid FromUomId, Guid ToUomId, decimal Quantity) : IRequest<ConvertedQuantityDto?>;

public record ConvertedQuantityDto(decimal Quantity, int ToDecimalPlaces);

/// <summary>
/// Quantity in the "from" unit expressed in the "to" unit: <c>q x from.factor / to.factor</c>, both factors being relative to the same
/// base unit (a unit without BaseUnitId is its own base, factor 1). Units of different bases (kg to litre) are refused with HTTP 400;
/// an unknown unit gives null (404). The result is not rounded - the caller rounds to <c>ToDecimalPlaces</c> where its rule says so.
/// </summary>
public class ConvertUnitQueryHandler(IUnitOfMeasureRepository units) : IRequestHandler<ConvertUnitQuery, ConvertedQuantityDto?>
{
    public async Task<ConvertedQuantityDto?> Handle(ConvertUnitQuery request, CancellationToken ct)
    {
        var found = await units.GetByIdsAsync([request.FromUomId, request.ToUomId], ct);
        var from = found.FirstOrDefault(u => u.Id == request.FromUomId);
        var to = found.FirstOrDefault(u => u.Id == request.ToUomId);
        if (from is null || to is null) return null;

        if ((from.BaseUnitId ?? from.Id) != (to.BaseUnitId ?? to.Id))
            throw new BusinessRuleException("UOM_INCOMPATIBLE", $"Cannot convert {from.Code} to {to.Code}: they do not share a base unit.");
        if (to.ConversionFactor <= 0)
            throw new BusinessRuleException("UOM_INVALID_FACTOR", $"Unit {to.Code} has no valid conversion factor.");

        var factorFrom = from.BaseUnitId is null ? 1m : from.ConversionFactor;
        var factorTo = to.BaseUnitId is null ? 1m : to.ConversionFactor;
        return new ConvertedQuantityDto(request.Quantity * factorFrom / factorTo, to.DecimalPlaces);
    }
}
