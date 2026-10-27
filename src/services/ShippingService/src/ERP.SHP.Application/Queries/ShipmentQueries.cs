using ERP.SHP.Application.DTOs;
using MediatR;

namespace ERP.SHP.Application.Queries;

public class GetShipmentQuery : IRequest<ShipmentDto?>
{
    public Guid ShipmentId { get; set; }
}

public class TrackShipmentQuery : IRequest<List<TrackingEventDto>>
{
    public Guid ShipmentId { get; set; }
}

public class ListShipmentsByOrderQuery : IRequest<List<ShipmentDto>>
{
    public Guid OrderId { get; set; }
}

public class ListCarriersQuery : IRequest<List<CarrierDto>> { }

/// <summary>Shipments of the shop, newest first, for the admin list and the staff delivery list. <c>Search</c> matches tracking number, carrier order code and recipient name.</summary>
public class ListShipmentsQuery : IRequest<ShipmentListResult>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public string? Search { get; set; }
    /// <summary>Only shipments flagged for a person (cash mismatch, carrier refused to cancel, ...).</summary>
    public bool NeedsReview { get; set; }
}
