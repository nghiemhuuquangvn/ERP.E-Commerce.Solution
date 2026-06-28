using ERP.SHP.Application.DTOs;
using MediatR;

namespace ERP.SHP.Application.Commands;

public class CreateShipmentCommand : IRequest<ShipmentDto>
{
    public Guid OrderId { get; set; }
    public Guid CarrierId { get; set; }
    public string ShippingAddress { get; set; } = string.Empty;
    public DateTime? EstimatedDelivery { get; set; }
    public List<ShipmentItemRequest> Items { get; set; } = new();
}

public class ShipmentItemRequest
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Weight { get; set; }
}

public class AssignTrackingNumberCommand : IRequest<ShipmentDto>
{
    public Guid ShipmentId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
}

public class AddTrackingEventCommand : IRequest<ShipmentDto>
{
    public Guid ShipmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class ConfirmDeliveryCommand : IRequest<ShipmentDto>
{
    public Guid ShipmentId { get; set; }
    public string Location { get; set; } = string.Empty;
}
