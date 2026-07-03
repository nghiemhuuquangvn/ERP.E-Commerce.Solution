using ERP.SHP.Application.Interfaces;
using MediatR;

namespace ERP.SHP.Application.Queries;

public class CarrierQuoteDto
{
    public decimal Rate { get; set; }
    public DateTime EstimatedDelivery { get; set; }
}

public class GetCarrierQuoteQuery : IRequest<CarrierQuoteDto>
{
    public Guid CarrierId { get; set; }
    public decimal WeightKg { get; set; }
    public string? DestinationProvince { get; set; }
}

/// <summary>
/// Carrier.CalculateRate/EstimateDeliveryDate existed before but were never called by
/// anything — the exposed "calculate-fee" endpoint went straight to ICarrierClient
/// (GHN's own pricing API) instead. This surfaces the in-repo domain quote (base rate
/// per kg + same/cross-province surcharge) as its own real, reachable endpoint.
/// </summary>
public class GetCarrierQuoteQueryHandler : IRequestHandler<GetCarrierQuoteQuery, CarrierQuoteDto>
{
    private readonly ICarrierRepository _carriers;
    public GetCarrierQuoteQueryHandler(ICarrierRepository carriers) => _carriers = carriers;

    public async Task<CarrierQuoteDto> Handle(GetCarrierQuoteQuery request, CancellationToken cancellationToken)
    {
        var carrier = await _carriers.GetByIdAsync(request.CarrierId, cancellationToken)
            ?? throw new InvalidOperationException($"Carrier {request.CarrierId} not found.");

        return new CarrierQuoteDto
        {
            Rate = carrier.CalculateRate(request.WeightKg, request.DestinationProvince),
            EstimatedDelivery = carrier.EstimateDeliveryDate(request.DestinationProvince)
        };
    }
}
