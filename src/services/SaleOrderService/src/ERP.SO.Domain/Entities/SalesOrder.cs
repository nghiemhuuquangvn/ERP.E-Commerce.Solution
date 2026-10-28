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
        public Address ShippingAddress { get; private set; } = null!;
        public string? Notes { get; private set; }

        /// <summary>Human-readable order number (e.g. SO-202607-00001), assigned once at creation via IOrderNumberGenerator.</summary>
        public string OrderNo { get; private set; } = string.Empty;
        public OrderSource OrderSource { get; private set; } = OrderSource.Manual;
        /// <summary>Opaque reference for dashboard segmentation — no local Department entity/lookup.</summary>
        public Guid? DepartmentId { get; private set; }
        /// <summary>Opaque reference to IdentityService's User — drives Commission attribution.</summary>
        public Guid? SalesRepId { get; private set; }
        /// <summary>Set only when this order was created by converting a SalesQuotation.</summary>
        public Guid? QuotationId { get; private set; }
        /// <summary>What the customer chose to pay with: COD, VNPay, MoMo, ZaloPay, or null / "Unspecified" (an order that did not say; it keeps the pre-P1-10 behaviour of waiting for a payment).</summary>
        public string? PaymentMethod { get; private set; }
        /// <summary>The recipient's phone: the carriers will not take a parcel without it (ROADMAP P1-11). Null on orders before it existed.</summary>
        public string? CustomerPhone { get; private set; }
        /// <summary>Cash on delivery: the order does not wait for a payment, the money is collected when the goods are delivered.</summary>
        public bool IsCashOnDelivery => string.Equals(PaymentMethod, "COD", StringComparison.OrdinalIgnoreCase);
        /// <summary>Which promotion produced DiscountAmount, if any — was previously discarded after coupon validation.</summary>
        public Guid? PromotionId { get; private set; }
        public decimal DiscountAmount { get; private set; }
        /// <summary>What the customer pays for delivery (the shop's flat fee, free from a threshold - ROADMAP P1-11b). Part of TotalAmount; decided by ShippingService when the order is created.</summary>
        public decimal ShippingFee { get; private set; }
        /// <summary>The goods after the coupon, without delivery.</summary>
        public decimal MerchandiseTotal => Math.Max(0, _lineItems.Sum(li => li.LineTotal) - DiscountAmount);

        public DateTime? ApprovedAt { get; private set; }
        public DateTime? ShippedAt { get; private set; }
        public DateTime? DeliveredAt { get; private set; }
        public DateTime? CancelledAt { get; private set; }
        public string? CancellationReason { get; private set; }

        public string? TrackingNumber { get; private set; }
        public string? Carrier { get; private set; }

        public DateTime? ReturnRequestedAt { get; private set; }
        public DateTime? ReturnedAt { get; private set; }
        public string? ReturnReason { get; private set; }
        public decimal? RefundAmount { get; private set; }
        /// <summary>True when the approver gave the delivery fee back with the goods (ROADMAP P1-11c); by default a return refunds the goods only.</summary>
        public bool ReturnShippingFeeRefunded { get; private set; }

        // P1-11d: the way back through the carrier.
        /// <summary>The approver approved the return (Shipping was asked to book the pickup).</summary>
        public DateTime? ReturnApprovedAt { get; private set; }
        /// <summary>A reverse parcel is booked at a carrier: the refund waits until the goods are back in the warehouse.</summary>
        public bool ReturnPickupBooked { get; private set; }
        public DateTime? ReturnGoodsReceivedAt { get; private set; }
        /// <summary>What Shipping answered when the return was approved (parcels booked, or why none).</summary>
        public string? ReturnPickupNote { get; private set; }

        /// <summary>Set when a ProductPriceChangedDomainEvent arrives for a product still on this (not-yet-Shipped/Delivered/Cancelled/Returned) order — see the note appended to InternalNotes for details.</summary>
        public bool HasPriceChangeWarning { get; private set; }

        private List<OrderLineItem> _lineItems = new();
        public IReadOnlyCollection<OrderLineItem> LineItems => _lineItems.AsReadOnly();

        private List<OrderNote> _internalNotes = new();
        public IReadOnlyCollection<OrderNote> InternalNotes => _internalNotes.AsReadOnly();

        private List<OrderStatusHistoryEntry> _statusHistory = new();
        public IReadOnlyCollection<OrderStatusHistoryEntry> StatusHistory => _statusHistory.AsReadOnly();

        protected SalesOrder() { }

        public static SalesOrder Create(
            Guid customerId,
            string customerName,
            string customerEmail,
            Address shippingAddress,
            List<OrderLineItem> lineItems,
            string? notes = null,
            string orderNo = "",
            OrderSource orderSource = OrderSource.Manual,
            Guid? departmentId = null,
            Guid? salesRepId = null,
            Guid? quotationId = null,
            string? paymentMethod = null,
            string? customerPhone = null)
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
                OrderNo = orderNo,
                OrderSource = orderSource,
                DepartmentId = departmentId,
                SalesRepId = salesRepId,
                QuotationId = quotationId,
                PaymentMethod = paymentMethod,
                CustomerPhone = string.IsNullOrWhiteSpace(customerPhone) ? null : customerPhone.Trim(),
                _lineItems = lineItems
            };

            order.RecalculateTotal();
            order.AddDomainEvent(new SalesOrderCreatedEvent
            {
                OrderId = order.Id,
                CustomerId = customerId,
                CustomerName = customerName,
                CustomerEmail = customerEmail,
                OrderNo = orderNo,
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

        private void RecordStatusChange(OrderStatus from, OrderStatus to, string? reason = null, string? changedBy = null)
        {
            _statusHistory.Add(new OrderStatusHistoryEntry(from, to, reason, changedBy));
        }

        public void Submit()
        {
            if (Status != OrderStatus.Draft)
                throw new InvalidOperationException($"Cannot submit order in {Status} status");

            var from = Status;
            Status = OrderStatus.Submitted;
            RecordStatusChange(from, Status);
        }

        public void Approve()
        {
            if (Status != OrderStatus.Submitted)
                throw new InvalidOperationException($"Cannot approve order in {Status} status");

            var from = Status;
            Status = OrderStatus.Approved;
            ApprovedAt = DateTime.UtcNow;
            RecordStatusChange(from, Status);

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

            var from = Status;
            Status = OrderStatus.Rejected;
            CancellationReason = reason;
            CancelledAt = DateTime.UtcNow;
            RecordStatusChange(from, Status, reason);
        }

        public void MarkAsPicked()
        {
            if (Status != OrderStatus.Approved)
                throw new InvalidOperationException($"Cannot mark as picked. Current status: {Status}");

            var from = Status;
            Status = OrderStatus.Picked;
            RecordStatusChange(from, Status);
        }

        /// <summary>Ships every remaining unit of every line — the "ship it all at once" convenience path used by the pre-existing ShipOrderCommand.</summary>
        public void Ship(string trackingNumber, string carrier)
        {
            var allRemaining = _lineItems
                .Where(li => li.RemainingQuantity > 0)
                .ToDictionary(li => li.ProductId, li => li.RemainingQuantity);

            ShipLineItems(allRemaining, trackingNumber, carrier);
        }

        /// <summary>
        /// Partial fulfillment: ships only the given quantities per product. Order
        /// moves to PartiallyShipped if any line still has RemainingQuantity > 0
        /// afterwards, or to Shipped (raising the original SalesOrderShippedEvent,
        /// unchanged for existing consumers) once every line is fully shipped.
        /// </summary>
        public void ShipLineItems(IReadOnlyDictionary<Guid, int> productQuantities, string trackingNumber, string carrier)
        {
            if (Status != OrderStatus.Picked && Status != OrderStatus.PartiallyShipped)
                throw new InvalidOperationException($"Cannot ship order in {Status} status");

            if (productQuantities.Count == 0)
                throw new ArgumentException("At least one line item quantity is required", nameof(productQuantities));

            foreach (var (productId, quantity) in productQuantities)
            {
                var line = _lineItems.FirstOrDefault(li => li.ProductId == productId)
                    ?? throw new InvalidOperationException($"Product {productId} is not on this order");

                if (quantity <= 0 || quantity > line.RemainingQuantity)
                    throw new InvalidOperationException($"Invalid ship quantity for product {productId}: {quantity} (remaining: {line.RemainingQuantity})");

                line.ShippedQuantity += quantity;
            }

            var from = Status;
            TrackingNumber = trackingNumber;
            Carrier = carrier;

            var fullyShipped = _lineItems.All(li => li.RemainingQuantity == 0);
            if (fullyShipped)
            {
                Status = OrderStatus.Shipped;
                ShippedAt = DateTime.UtcNow;
                RecordStatusChange(from, Status);

                AddDomainEvent(new SalesOrderShippedEvent
                {
                    OrderId = Id,
                    CustomerId = CustomerId,
                    TrackingNumber = trackingNumber,
                    Carrier = carrier,
                    ShippedAt = ShippedAt.Value
                });
            }
            else
            {
                Status = OrderStatus.PartiallyShipped;
                RecordStatusChange(from, Status);

                AddDomainEvent(new SalesOrderPartiallyShippedEvent
                {
                    OrderId = Id,
                    CustomerId = CustomerId,
                    TrackingNumber = trackingNumber,
                    Carrier = carrier,
                    ShippedLines = productQuantities.Select(kv => new SalesOrderPartiallyShippedEvent.ShippedLine { ProductId = kv.Key, Quantity = kv.Value }).ToList(),
                    ShippedAt = DateTime.UtcNow
                });
            }
        }

        public void MarkAsDelivered(string? reason = null)
        {
            if (Status != OrderStatus.Shipped)
                throw new InvalidOperationException($"Cannot mark as delivered. Current status: {Status}");

            var from = Status;
            Status = OrderStatus.Delivered;
            DeliveredAt = DateTime.UtcNow;
            RecordStatusChange(from, Status, reason);

            AddDomainEvent(new SalesOrderDeliveredEvent
            {
                OrderId = Id,
                CustomerId = CustomerId,
                TotalAmount = TotalAmount,
                DeliveredAt = DeliveredAt.Value
            });
        }

        public void Cancel(string reason)
        {
            if (Status is OrderStatus.Shipped or OrderStatus.PartiallyShipped or OrderStatus.Delivered or OrderStatus.Cancelled or OrderStatus.ReturnRequested or OrderStatus.Returned)
                throw new InvalidOperationException($"Cannot cancel order in {Status} status");

            var from = Status;
            Status = OrderStatus.Cancelled;
            CancellationReason = reason;
            CancelledAt = DateTime.UtcNow;
            RecordStatusChange(from, Status, reason);

            AddDomainEvent(new SalesOrderCancelledEvent
            {
                OrderId = Id,
                CustomerId = CustomerId,
                CancellationReason = reason,
                CancelledAt = CancelledAt.Value
            });
        }

        /// <summary>Only allowed before processing starts — once Approved/Picked/Shipped, the address/notes are what fulfillment is already working from.</summary>
        public void UpdateOrder(Address? shippingAddress, string? notes)
        {
            if (Status != OrderStatus.Draft && Status != OrderStatus.Submitted)
                throw new InvalidOperationException($"Cannot update order in {Status} status");

            if (shippingAddress != null)
                ShippingAddress = shippingAddress;
            if (notes != null)
                Notes = notes;
        }

        public void AddInternalNote(string note, string? addedBy)
        {
            if (string.IsNullOrWhiteSpace(note))
                throw new ArgumentException("Note cannot be empty", nameof(note));

            _internalNotes.Add(new OrderNote(note.Trim(), addedBy));
        }

        /// <summary>Only meaningful before the order has shipped — after that the price a customer already paid isn't retroactively changed by a later catalog price update.</summary>
        public void FlagPriceChanged(Guid productId, decimal oldPrice, decimal newPrice)
        {
            if (Status is OrderStatus.Shipped or OrderStatus.PartiallyShipped or OrderStatus.Delivered or OrderStatus.Cancelled or OrderStatus.Returned)
                return;

            if (!_lineItems.Any(li => li.ProductId == productId))
                return;

            HasPriceChangeWarning = true;
            AddInternalNote($"Price for product {productId} changed from {oldPrice:C} to {newPrice:C} after this order was placed.", "system:product-price-sync");
        }

        public void RequestReturn(string reason)
        {
            if (Status != OrderStatus.Delivered)
                throw new InvalidOperationException($"Cannot request a return for an order in {Status} status");

            var from = Status;
            Status = OrderStatus.ReturnRequested;
            ReturnRequestedAt = DateTime.UtcNow;
            ReturnReason = reason;
            RecordStatusChange(from, Status, reason);
        }

        /// <summary>
        /// The approver accepts the return; <paramref name="pickupBooked"/> says a carrier will fetch the goods (then the refund waits for them). Asking again is allowed
        /// (a refused booking is retried) and never un-books a pickup.
        /// </summary>
        public void ApproveReturn(bool pickupBooked, string? note)
        {
            if (Status != OrderStatus.ReturnRequested)
                throw new InvalidOperationException($"Cannot approve a return for an order in {Status} status");
            ReturnApprovedAt ??= DateTime.UtcNow;
            ReturnPickupBooked |= pickupBooked;
            ReturnPickupNote = note is { Length: > 1000 } ? note[..1000] : note;
        }

        /// <summary>Shipping says every reverse parcel arrived. Counts only for a return waiting on a booked pickup; repeated messages change nothing.</summary>
        public void MarkReturnGoodsReceived()
        {
            if (Status != OrderStatus.ReturnRequested || !ReturnPickupBooked || ReturnGoodsReceivedAt.HasValue) return;
            ReturnGoodsReceivedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Records that the return finished and a refund of the given amount is owed —
        /// PaymentService/AccountingService react to SalesOrderReturnedEvent to actually
        /// process the refund/reverse the journal entry; this method does not call them
        /// directly (cross-service side effects happen via the published event, not a
        /// synchronous call, same as every other state-changing flow in this system).
        /// </summary>
        public void CompleteReturn(decimal refundAmount, bool refundShippingFee = false)
        {
            if (Status != OrderStatus.ReturnRequested)
                throw new InvalidOperationException($"Cannot complete a return for an order in {Status} status");

            // A carrier is bringing the goods back: the money goes out when they are in the warehouse (a person can receive them by hand in Shipping).
            if (ReturnPickupBooked && !ReturnGoodsReceivedAt.HasValue)
                throw new InvalidOperationException("The goods are still on their way back: the refund can be completed when the warehouse has received them.");

            // The amount is typed by the approver; the ledger credits the customer with exactly this figure (ROADMAP P1-1e).
            // The delivery fee comes back only when the approver says so (ROADMAP P1-11c): the cap is the goods, plus the fee when it is ticked.
            var limit = MerchandiseTotal + (refundShippingFee ? ShippingFee : 0m);
            if (refundAmount <= 0m || refundAmount > limit)
                throw new InvalidOperationException($"The refund amount must be greater than 0 and at most {limit} (the goods{(ShippingFee > 0m ? (refundShippingFee ? " and the delivery fee" : "; the delivery fee is refunded only when it is ticked") : "")}).");

            var from = Status;
            Status = OrderStatus.Returned;
            ReturnedAt = DateTime.UtcNow;
            RefundAmount = refundAmount;
            ReturnShippingFeeRefunded = refundShippingFee && ShippingFee > 0m;
            RecordStatusChange(from, Status);

            AddDomainEvent(new SalesOrderReturnedEvent
            {
                OrderId = Id,
                CustomerId = CustomerId,
                Reason = ReturnReason ?? string.Empty,
                RefundAmount = refundAmount,
                ShippingFeeRefunded = ReturnShippingFeeRefunded,
                ReturnedAt = ReturnedAt.Value,
                Lines = _lineItems.Where(li => li.ShippedQuantity > 0)
                    .Select(li => new SalesOrderReturnedEvent.ReturnedLine { ProductId = li.ProductId, Quantity = li.ShippedQuantity }).ToList()
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

        /// <summary>Applies a flat discount amount (from a validated coupon) at order creation, before the first save — line item prices are left as-is, only TotalAmount reflects the discount. Also persists which promotion produced it (previously discarded after validation).</summary>
        public void ApplyDiscount(Guid promotionId, decimal discountAmount)
        {
            if (discountAmount < 0)
                throw new ArgumentException("Discount cannot be negative", nameof(discountAmount));

            PromotionId = promotionId;
            DiscountAmount = discountAmount;
            RecalculateTotal();
        }

        /// <summary>Sets the delivery fee the customer pays and puts it in the total. Before the first save only: the order created event the other services read carries it.</summary>
        public void SetShippingFee(decimal fee)
        {
            if (fee < 0)
                throw new ArgumentException("The shipping fee cannot be negative", nameof(fee));

            ShippingFee = fee;
            RecalculateTotal();
        }

        private void RecalculateTotal()
        {
            TotalAmount = MerchandiseTotal + ShippingFee;

            // The order created event is built before the coupon and the delivery fee are known: keep it equal to the order that is saved.
            foreach (var created in DomainEvents.OfType<SalesOrderCreatedEvent>())
            {
                created.TotalAmount = TotalAmount;
                created.ShippingFee = ShippingFee;
                created.DiscountAmount = DiscountAmount;
            }
        }
    }
}
