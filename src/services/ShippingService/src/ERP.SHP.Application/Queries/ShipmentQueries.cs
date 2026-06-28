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
