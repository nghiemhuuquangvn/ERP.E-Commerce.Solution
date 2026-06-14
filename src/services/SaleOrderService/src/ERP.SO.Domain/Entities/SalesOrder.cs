using ERP.Shared.Core.Domain;
using ERP.SO.Domain.Events;
using ERP.SO.Domain.ValueObjects;

namespace ERP.SO.Domain.Entities
{
    public class SalesOrder : AggregateRoot
    {
        public Guid CustomerId { get; private set; }
        public string CustomerName { get; private set; } = string.Empty;
        public string CustomerEmail { get; private set; } = string.Empty;
        public DateTime OrderDate { get; private set; }
        public OrderStatus Status { get; private set; }
        public decimal TotalAmount { get; private set; }
        public Address ShippingAddress { get; private set; }
        public string? Notes { get; private set; }

        public DateTime? ApprovedAt { get; private set; }
        public DateTime? ShippedAt { get; private set; }
        public DateTime? DeliveredAt { get; private set; }
        public DateTime? CancelledAt { get; private set; }
        public string? CancellationReason { get; private set; }

        private List<OrderLineItem> _lineItems = new();
        public IReadOnlyCollection<OrderLineItem> LineItems => _lineItems.AsReadOnly();

        protected SalesOrder() { }

        public static SalesOrder Create(
            Guid customerId,
            string customerName,
            string customerEmail,
            Address shippingAddress,
            List<OrderLineItem> lineItems,
            string? notes = null)
        {
            var order = new SalesOrder
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                CustomerName = customerName,
                CustomerEmail = customerEmail,
                OrderDate = DateTime.UtcNow,
                Status = OrderStatus.Draft,
                ShippingAddress = shippingAddress,
                Notes = notes,
                _lineItems = lineItems
            };

            order.RecalculateTotal();
            order.AddDomainEvent(new SalesOrderCreatedEvent
            {
                OrderId = order.Id,
                CustomerId = customerId,
                CustomerName = customerName,
                OrderDate = order.OrderDate,
                TotalAmount = order.TotalAmount,
                LineItems = lineItems.Select(li => new SalesOrderCreatedEvent.LineItem
                {
                    ProductId = li.ProductId,
                    Quantity = li.Quantity,
                    UnitPrice = li.UnitPrice
                }).ToList(),
                Status = order.Status,
                CreatedAt = DateTime.UtcNow
            });

            return order;
        }

        public void Submit()
        {
            if (Status != OrderStatus.Draft)
                throw new InvalidOperationException($"Cannot submit order in {Status} status");

            Status = OrderStatus.Submitted;
        }

        public void Approve()
        {
            if (Status != OrderStatus.Submitted)
                throw new InvalidOperationException($"Cannot approve order in {Status} status");

            Status = OrderStatus.Approved;
            ApprovedAt = DateTime.UtcNow;

            AddDomainEvent(new SalesOrderApprovedEvent
            {
                OrderId = Id,
                CustomerId = CustomerId,
                ApprovedAt = ApprovedAt.Value,
                TotalAmount = TotalAmount
            });
        }

        public void Reject(string reason)
        {
            if (Status != OrderStatus.Submitted)
                throw new InvalidOperationException($"Cannot reject order in {Status} status");

            Status = OrderStatus.Rejected;
            CancellationReason = reason;
            CancelledAt = DateTime.UtcNow;
        }

        public void MarkAsPicked()
        {
            if (Status != OrderStatus.Approved)
                throw new InvalidOperationException($"Cannot mark as picked. Current status: {Status}");

            Status = OrderStatus.Picked;
        }

        public void Ship(string trackingNumber, string carrier)
        {
            if (Status != OrderStatus.Picked)
                throw new InvalidOperationException($"Cannot ship order in {Status} status");

            Status = OrderStatus.Shipped;
            ShippedAt = DateTime.UtcNow;

            AddDomainEvent(new SalesOrderShippedEvent
            {
                OrderId = Id,
                CustomerId = CustomerId,
                TrackingNumber = trackingNumber,
                Carrier = carrier,
                ShippedAt = ShippedAt.Value
            });
        }

        public void MarkAsDelivered()
        {
            if (Status != OrderStatus.Shipped)
                throw new InvalidOperationException($"Cannot mark as delivered. Current status: {Status}");

            Status = OrderStatus.Delivered;
            DeliveredAt = DateTime.UtcNow;
        }

        public void Cancel(string reason)
        {
            if (Status is OrderStatus.Shipped or OrderStatus.Delivered or OrderStatus.Cancelled)
                throw new InvalidOperationException($"Cannot cancel order in {Status} status");

            Status = OrderStatus.Cancelled;
            CancellationReason = reason;
            CancelledAt = DateTime.UtcNow;

            AddDomainEvent(new SalesOrderCancelledEvent
            {
                OrderId = Id,
                CustomerId = CustomerId,
                CancellationReason = reason,
                CancelledAt = CancelledAt.Value
            });
        }

        public void AddLineItem(OrderLineItem item)
        {
            if (Status != OrderStatus.Draft)
                throw new InvalidOperationException("Cannot add items to order that's not in Draft status");

            _lineItems.Add(item);
            RecalculateTotal();
        }

        public void RemoveLineItem(Guid productId)
        {
            if (Status != OrderStatus.Draft)
                throw new InvalidOperationException("Cannot remove items from order that's not in Draft status");

            _lineItems.RemoveAll(li => li.ProductId == productId);
            RecalculateTotal();
        }

        private void RecalculateTotal()
        {
            TotalAmount = _lineItems.Sum(li => li.LineTotal);
        }
    }
}
