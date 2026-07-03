using ERP.SHP.Application.DTOs;
using MediatR;

namespace ERP.SHP.Application.Commands;

public class ShipmentRecipientRequest
{
    public string RecipientName { get; set; } = string.Empty;
    public string? RecipientPhone { get; set; }
    public string AddressStreet { get; set; } = string.Empty;
    public string? AddressWard { get; set; }
    public string AddressDistrict { get; set; } = string.Empty;
    public string AddressProvince { get; set; } = string.Empty;
    public string? AddressPostalCode { get; set; }
    public string AddressCountry { get; set; } = "Vietnam";
}

public class CreateShipmentCommand : IRequest<ShipmentDto>
{
    public Guid OrderId { get; set; }
    public Guid CarrierId { get; set; }
    public ShipmentRecipientRequest Recipient { get; set; } = new();
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
    public string? ReceivedByName { get; set; }
    public string? SignatureUrl { get; set; }
    public string? PhotoUrl { get; set; }
}

public class MarkDeliveryFailedCommand : IRequest<ShipmentDto>
{
    public Guid ShipmentId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class RetryDeliveryCommand : IRequest<ShipmentDto>
{
    public Guid ShipmentId { get; set; }
}

public class BulkTrackingUpdateItem
{
    public Guid ShipmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class BulkUpdateTrackingCommand : IRequest<List<ShipmentDto>>
{
    public List<BulkTrackingUpdateItem> Updates { get; set; } = new();
}
