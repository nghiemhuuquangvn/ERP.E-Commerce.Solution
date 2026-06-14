using MediatR;

namespace ERP.WH.Application.Commands.Inventory;

public class ReserveInventoryCommand : IRequest<ReserveInventoryResponse>
{
    public Guid OrderId { get; set; }
    public List<ReservationItemDto> Items { get; set; }
    public DateTime HoldUntil { get; set; }
    public string CorrelationId { get; set; }
}

public class ReservationItemDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}

public class ReserveInventoryResponse
{
    public Guid ReservationId { get; set; }
    public Guid OrderId { get; set; }
    public List<ReservedItemResponse> ReservedItems { get; set; }
    public string Status { get; set; }
    public DateTime ReservedAt { get; set; }
}

public class ReservedItemResponse
{
    public Guid ProductId { get; set; }
    public int ReservedQuantity { get; set; }
    public string Location { get; set; }
}
