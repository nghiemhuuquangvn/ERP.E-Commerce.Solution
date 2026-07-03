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
        public string? CouponCode { get; set; }

        public CreateOrderCommand() { }

        public CreateOrderCommand(CreateOrderRequest request)
        {
            CustomerId = request.CustomerId;
            CustomerName = request.CustomerName;
            CustomerEmail = request.CustomerEmail;
            LineItems = request.LineItems;
            ShippingAddress = request.ShippingAddress;
            Notes = request.Notes;
            CouponCode = request.CouponCode;
        }
    }

    public class UpdateOrderCommand : IRequest<SalesOrderDto>
    {
        public Guid OrderId { get; set; }
        public AddressDto? ShippingAddress { get; set; }
        public string? Notes { get; set; }
    }

    public class AddOrderNoteCommand : IRequest<Unit>
    {
        public Guid OrderId { get; set; }
        public string Note { get; set; } = string.Empty;
        public string? AddedBy { get; set; }
    }

    public class ShipLineItemsCommand : IRequest<SalesOrderDto>
    {
        public Guid OrderId { get; set; }
        public Dictionary<Guid, int> Quantities { get; set; } = new();
        public string TrackingNumber { get; set; } = string.Empty;
        public string Carrier { get; set; } = string.Empty;
    }

    public class RequestReturnCommand : IRequest<SalesOrderDto>
    {
        public Guid OrderId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class CompleteReturnCommand : IRequest<SalesOrderDto>
    {
        public Guid OrderId { get; set; }
        public decimal RefundAmount { get; set; }
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

    public class RejectOrderCommand : IRequest<SalesOrderDto>
    {
        public Guid OrderId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class MarkAsPickedCommand : IRequest<SalesOrderDto>
    {
        public Guid OrderId { get; set; }
    }
}
