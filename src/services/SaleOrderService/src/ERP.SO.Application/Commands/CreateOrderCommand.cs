using ERP.SO.Application.DTOs;
using MediatR;

namespace ERP.SO.Application.Commands
{
    public class CreateOrderCommand : IRequest<SalesOrderDto>
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public List<OrderLineItemDto> LineItems { get; set; } = new();
        public AddressDto ShippingAddress { get; set; } = new();
        public string? Notes { get; set; }

        public CreateOrderCommand() { }

        public CreateOrderCommand(CreateOrderRequest request)
        {
            CustomerId = request.CustomerId;
            CustomerName = request.CustomerName;
            CustomerEmail = request.CustomerEmail;
            LineItems = request.LineItems;
            ShippingAddress = request.ShippingAddress;
            Notes = request.Notes;
        }
    }

    public class ApproveOrderCommand : IRequest<SalesOrderDto>
    {
        public Guid OrderId { get; set; }
    }

    public class ShipOrderCommand : IRequest<SalesOrderDto>
    {
        public Guid OrderId { get; set; }
        public string TrackingNumber { get; set; } = string.Empty;
        public string Carrier { get; set; } = string.Empty;
    }

    public class CancelOrderCommand : IRequest<SalesOrderDto>
    {
        public Guid OrderId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class SubmitOrderCommand : IRequest<SalesOrderDto>
    {
        public Guid OrderId { get; set; }
    }

    public class MarkAsDeliveredCommand : IRequest<SalesOrderDto>
    {
        public Guid OrderId { get; set; }
    }
}
