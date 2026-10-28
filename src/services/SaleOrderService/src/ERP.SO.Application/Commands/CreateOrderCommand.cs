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
        public string? OrderSource { get; set; }
        public Guid? DepartmentId { get; set; }
        public Guid? SalesRepId { get; set; }
        public Guid? QuotationId { get; set; }
        public string? PaymentMethod { get; set; }
        public string? CustomerPhone { get; set; }

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
            OrderSource = request.OrderSource;
            DepartmentId = request.DepartmentId;
            SalesRepId = request.SalesRepId;
            PaymentMethod = request.PaymentMethod;
            CustomerPhone = request.CustomerPhone;
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

    /// <summary>The approver accepts the return; Shipping is asked to book the goods' way back at a carrier (ROADMAP P1-11d). Safe to repeat.</summary>
    public class ApproveReturnCommand : IRequest<SalesOrderDto>
    {
        public Guid OrderId { get; set; }
    }

    /// <summary>Shipping told us every reverse parcel of the order was received.</summary>
    public class MarkReturnGoodsReceivedCommand : IRequest<Unit>
    {
        public Guid OrderId { get; set; }
    }

    public class CompleteReturnCommand : IRequest<SalesOrderDto>
    {
        public Guid OrderId { get; set; }
        public decimal RefundAmount { get; set; }
        /// <summary>The approver also gives the delivery fee back (ROADMAP P1-11c); the refund may then exceed the goods by up to the fee.</summary>
        public bool RefundShippingFee { get; set; }
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

        /// <summary>Why, for the status history (the carrier's confirmation sets it, ROADMAP P1-5).</summary>
        public string? Reason { get; set; }
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
