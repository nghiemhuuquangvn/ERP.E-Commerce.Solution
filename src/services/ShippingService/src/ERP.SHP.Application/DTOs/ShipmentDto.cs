namespace ERP.SHP.Application.DTOs;

public class ShipmentDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string? TrackingNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string? RecipientPhone { get; set; }
    public string AddressStreet { get; set; } = string.Empty;
    public string? AddressWard { get; set; }
    public string AddressDistrict { get; set; } = string.Empty;
    public string AddressProvince { get; set; } = string.Empty;
    public string? AddressPostalCode { get; set; }
    public string AddressCountry { get; set; } = string.Empty;
    public DateTime? EstimatedDelivery { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public int DeliveryAttempts { get; set; }
    public string? ReceivedByName { get; set; }
    public string? SignatureUrl { get; set; }
    public string? PhotoUrl { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal CodAmount { get; set; }
    public decimal DeclaredValue { get; set; }
    public decimal? CarrierFee { get; set; }
    public string? CarrierOrderCode { get; set; }
    public string? SelectionNote { get; set; }
    public string? BookingError { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? LastCarrierSyncAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TrackingEventDto> TrackingEvents { get; set; } = new();
    public List<ShipmentItemDto> Items { get; set; } = new();
}

public class ShipmentListResult
{
    public List<ShipmentDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public class ReturnShipmentDto
{
    public Guid Id { get; set; }
    public Guid OriginalShipmentId { get; set; }
    public Guid OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? TrackingNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PickedUpAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TrackingEventDto
{
    public string Status { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}

public class ShipmentItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Weight { get; set; }
}

public class CarrierDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public decimal BaseRatePerKg { get; set; }
}

/// <summary>A parcel to price with every carrier the shop can use (admin quote panel).</summary>
public class QuoteParcelRequest
{
    public string Province { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string? Ward { get; set; }
    public string? Street { get; set; }
    public int WeightGrams { get; set; } = 1000;
    public decimal DeclaredValue { get; set; }
    public decimal CodAmount { get; set; }
}
