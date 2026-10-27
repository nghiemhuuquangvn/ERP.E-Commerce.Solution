using ERP.Shared.Messaging;
using ERP.SO.Application.Commands;
using ERP.SO.Application.Interfaces;
using ERP.SO.Domain.Sagas;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.SO.Application.Sagas;

/// <summary>
/// Orchestrates the Order Fulfillment Saga.
///
/// Flow: Order Created → Payment Initiated → Payment Processed → Inventory Reserved
/// → (wait for both Inventory Reserved AND manual order Approval) → Shipment Created
/// → Completed. Compensation: a payment failure or insufficient inventory cancels the
/// order (in-process — SO owns that command) and, if a payment already exists,
/// requests a refund from PaymentService.
///
/// Persistence and idempotency: every transition is followed by a single
/// <see cref="ISalesOrderUnitOfWork.SaveChangesAsync"/> call — omitting this was the
/// reason this saga previously discarded all of its state. Every Handle*Async method
/// also guards on <see cref="OrderFulfillmentSagaState.CurrentStep"/> before mutating,
/// so a redelivered message (Kafka auto-commit, RabbitMQ nack-requeue) is a no-op
/// instead of double-publishing the next command.
/// </summary>
public sealed class OrderFulfillmentSaga
{
    private readonly ISalesOrderUnitOfWork _uow;
    private readonly IMessageBus _messageBus;
    private readonly IMediator _mediator;
    private readonly ILogger<OrderFulfillmentSaga> _logger;

    public OrderFulfillmentSaga(
        ISalesOrderUnitOfWork uow,
        IMessageBus messageBus,
        IMediator mediator,
        ILogger<OrderFulfillmentSaga> logger)
    {
        _uow = uow;
        _messageBus = messageBus;
        _mediator = mediator;
        _logger = logger;
    }

    // ── Step 1: Order created → start saga + request payment ──────────────
    public async Task HandleOrderCreatedAsync(Guid orderId, Guid tenantId, decimal totalAmount, CancellationToken ct,
        Guid? customerId = null, string? orderNumber = null, string? paymentMethod = null)
    {
        var existing = await _uow.Sagas.GetByOrderIdAsync(orderId, ct);
        if (existing is not null)
        {
            _logger.LogWarning("Saga already exists for Order {OrderId}", orderId);
            return;
        }

        var saga = OrderFulfillmentSagaState.Start(orderId, tenantId);
        await _uow.Sagas.AddAsync(saga, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Saga {SagaId} started for Order {OrderId} — requesting payment (Amount={Amount})",
            saga.Id, orderId, totalAmount);

        await _messageBus.PublishAsync(BuildInitiatePayment(orderId, tenantId, totalAmount, customerId, orderNumber, paymentMethod), ct);

        // Cash on delivery does not wait: Payment records a pending COD payment (captured when the goods are delivered) and the saga
        // goes on to reserve stock at once.
        if (string.Equals(paymentMethod, "COD", StringComparison.OrdinalIgnoreCase))
        {
            var order = await _uow.Orders.GetByIdAsync(orderId, ct);
            if (order is null) return;

            saga.SkipPaymentForCashOnDelivery();
            await _uow.Sagas.UpdateAsync(saga, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Saga {SagaId}: Order {OrderId} is cash on delivery — requesting inventory reservation without waiting for payment", saga.Id, orderId);
            await _messageBus.PublishAsync(new ReserveInventoryCommand
            {
                OrderId = orderId,
                TenantId = saga.TenantId,
                Items = order.LineItems.Select(li => new SagaLineItem { ProductId = li.ProductId, Quantity = li.Quantity }).ToList()
            }, ct);
        }
    }

    /// <summary>
    /// What Payment needs to make the payment the customer asked for. Online gateways settle VND only, so those orders (and COD) carry VND;
    /// an order that did not say keeps the old defaults (no currency, Payment picks its own).
    /// </summary>
    internal static InitiatePaymentCommand BuildInitiatePayment(Guid orderId, Guid tenantId, decimal amount, Guid? customerId, string? orderNumber, string? paymentMethod) => new()
    {
        OrderId = orderId,
        TenantId = tenantId,
        Amount = amount,
        PaymentMethod = paymentMethod,
        Currency = string.IsNullOrEmpty(paymentMethod) || string.Equals(paymentMethod, "Unspecified", StringComparison.OrdinalIgnoreCase) ? null : "VND",
        CustomerId = customerId,
        OrderNumber = orderNumber
    };

    /// <summary>
    /// Refunds the payment when the saga knows it. Without a payment id (cash on delivery, or an online order that timed out before it was paid) Payment is
    /// asked, by order id, to close whatever it holds: a pending payment is cancelled, so a late "paid" from the gateway can no longer be taken for this order.
    /// </summary>
    private async Task RefundOrCancelPaymentAsync(OrderFulfillmentSagaState saga, string reason, CancellationToken ct)
    {
        await _messageBus.PublishAsync(new RefundPaymentCommand
        {
            PaymentId = saga.PaymentId ?? Guid.Empty, OrderId = saga.OrderId, TenantId = saga.TenantId, Reason = reason
        }, ct);
    }

    // ── Step 2: Payment processed → reserve inventory ─────────────────────
    public async Task HandlePaymentProcessedAsync(Guid orderId, Guid paymentId, CancellationToken ct)
    {
        var saga = await _uow.Sagas.GetByOrderIdAsync(orderId, ct);
        if (saga is null)
        {
            _logger.LogError("No saga found for Order {OrderId} on PaymentProcessed", orderId);
            return;
        }

        if (saga.CurrentStep != SagaStep.PaymentPending)
        {
            _logger.LogInformation("Saga {SagaId} already past PaymentPending (at {Step}) — ignoring redelivered PaymentProcessed", saga.Id, saga.CurrentStep);
            return;
        }

        var order = await _uow.Orders.GetByIdAsync(orderId, ct);
        if (order is null)
        {
            _logger.LogError("Saga {SagaId}: Order {OrderId} not found while reserving inventory", saga.Id, orderId);
            return;
        }

        saga.PaymentInitiated(paymentId);
        await _uow.Sagas.UpdateAsync(saga, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Saga {SagaId}: payment {PaymentId} confirmed — requesting inventory reservation",
            saga.Id, paymentId);

        await _messageBus.PublishAsync(new ReserveInventoryCommand
        {
            OrderId = orderId,
            TenantId = saga.TenantId,
            Items = order.LineItems.Select(li => new SagaLineItem { ProductId = li.ProductId, Quantity = li.Quantity }).ToList()
        }, ct);
    }

    // ── Step 3a: Inventory reserved → advance to shipping once also approved ──
    public async Task HandleInventoryReservedAsync(Guid orderId, Guid reservationId, CancellationToken ct)
    {
        var saga = await _uow.Sagas.GetByOrderIdAsync(orderId, ct);
        if (saga is null)
        {
            _logger.LogError("No saga found for Order {OrderId} on InventoryReserved", orderId);
            return;
        }

        if (saga.CurrentStep != SagaStep.InventoryPending)
        {
            _logger.LogInformation("Saga {SagaId} already past InventoryPending (at {Step}) — ignoring redelivered InventoryReserved", saga.Id, saga.CurrentStep);
            return;
        }

        saga.RecordInventoryReserved(reservationId);
        var readyToShip = saga.TryAdvanceToShipping();
        await _uow.Sagas.UpdateAsync(saga, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Saga {SagaId}: inventory reserved ({ReservationId})", saga.Id, reservationId);

        if (readyToShip)
            await PublishCreateShipmentCommandAsync(saga, ct);
    }

    // ── Step 3b: Order approved (manual gate) → advance to shipping once also reserved ──
    public async Task HandleOrderApprovedAsync(Guid orderId, CancellationToken ct)
    {
        var saga = await _uow.Sagas.GetByOrderIdAsync(orderId, ct);
        if (saga is null)
        {
            _logger.LogWarning("No saga found for Order {OrderId} on manual Approve — order predates this feature or saga was never started", orderId);
            return;
        }

        if (saga.CurrentStep is SagaStep.ShippingPending or SagaStep.Completed or SagaStep.Compensating or SagaStep.Failed)
        {
            _logger.LogInformation("Saga {SagaId} at {Step} — Approve signal is a no-op", saga.Id, saga.CurrentStep);
            return;
        }

        saga.MarkApproved();
        var readyToShip = saga.TryAdvanceToShipping();
        await _uow.Sagas.UpdateAsync(saga, ct);
        await _uow.SaveChangesAsync(ct);

        if (readyToShip)
            await PublishCreateShipmentCommandAsync(saga, ct);
    }

    private async Task PublishCreateShipmentCommandAsync(OrderFulfillmentSagaState saga, CancellationToken ct)
    {
        _logger.LogInformation("Saga {SagaId}: approved and inventory reserved — requesting shipment creation", saga.Id);

        // Shipping has no copy of the order, so the command carries what the carrier needs:
        // recipient, delivery address and the lines still to ship. Without this Shipping
        // created every shipment with a "(pending address sync)" placeholder.
        var order = await _uow.Orders.GetByIdAsync(saga.OrderId, ct);
        if (order is null)
        {
            _logger.LogError("Saga {SagaId}: order {OrderId} not found — shipment not requested", saga.Id, saga.OrderId);
            return;
        }

        await _messageBus.PublishAsync(new CreateShipmentCommand
        {
            OrderId = saga.OrderId,
            TenantId = saga.TenantId,
            RecipientName = order.CustomerName,
            RecipientEmail = order.CustomerEmail,
            ShippingAddress = new SagaShippingAddress
            {
                Street = order.ShippingAddress.Street,
                City = order.ShippingAddress.City,
                State = order.ShippingAddress.State,
                PostalCode = order.ShippingAddress.PostalCode,
                Country = order.ShippingAddress.Country
            },
            Items = order.LineItems
                .Where(li => li.RemainingQuantity > 0)
                .Select(li => new SagaShipmentLine { ProductId = li.ProductId, ProductName = li.ProductName, Quantity = li.RemainingQuantity })
                .ToList()
        }, ct);
    }

    // ── Step 4: Shipment created → saga complete ──────────────────────────
    public async Task HandleShipmentCreatedAsync(Guid orderId, Guid shipmentId, CancellationToken ct)
    {
        var saga = await _uow.Sagas.GetByOrderIdAsync(orderId, ct);
        if (saga is null)
        {
            _logger.LogError("No saga found for Order {OrderId} on ShipmentCreated", orderId);
            return;
        }

        if (saga.CurrentStep != SagaStep.ShippingPending)
        {
            _logger.LogInformation("Saga {SagaId} not awaiting shipment (at {Step}) — ignoring redelivered ShipmentCreated", saga.Id, saga.CurrentStep);
            return;
        }

        saga.ShipmentCreated(shipmentId);
        await _uow.Sagas.UpdateAsync(saga, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Saga {SagaId} completed for Order {OrderId} in {Duration}ms",
            saga.Id, orderId, (saga.CompletedAt!.Value - saga.CreatedAt).TotalMilliseconds);
    }

    // ── Compensation: payment failed → cancel order (in-process) ──────────
    public async Task HandlePaymentFailedAsync(Guid orderId, string reason, CancellationToken ct)
    {
        var saga = await _uow.Sagas.GetByOrderIdAsync(orderId, ct);
        if (saga is null) return;

        if (saga.CurrentStep != SagaStep.PaymentPending)
        {
            _logger.LogInformation("Saga {SagaId} already past PaymentPending (at {Step}) — ignoring redelivered PaymentFailed", saga.Id, saga.CurrentStep);
            return;
        }

        _logger.LogWarning("Saga {SagaId}: payment failed for Order {OrderId} — {Reason}. Compensating.",
            saga.Id, orderId, reason);

        saga.BeginCompensation($"Payment failed: {reason}");
        saga.MarkFailed($"Payment failed: {reason}");
        await _uow.Sagas.UpdateAsync(saga, ct);
        await _uow.SaveChangesAsync(ct);

        await _mediator.Send(new CancelOrderCommand { OrderId = orderId, Reason = $"Payment failed: {reason}" }, ct);
    }

    // ── Compensation: inventory insufficient → refund (if paid) + cancel order ──
    public async Task HandleInventoryInsufficientAsync(Guid orderId, string reason, CancellationToken ct)
    {
        var saga = await _uow.Sagas.GetByOrderIdAsync(orderId, ct);
        if (saga is null) return;

        if (saga.CurrentStep != SagaStep.InventoryPending)
        {
            _logger.LogInformation("Saga {SagaId} not awaiting inventory (at {Step}) — ignoring redelivered InventoryInsufficient", saga.Id, saga.CurrentStep);
            return;
        }

        _logger.LogWarning("Saga {SagaId}: inventory insufficient for Order {OrderId} — {Reason}. Compensating.",
            saga.Id, orderId, reason);

        saga.BeginCompensation($"Inventory insufficient: {reason}");
        saga.MarkFailed($"Inventory insufficient: {reason}");
        await _uow.Sagas.UpdateAsync(saga, ct);
        await _uow.SaveChangesAsync(ct);

        await RefundOrCancelPaymentAsync(saga, "Order cancelled: inventory insufficient", ct);

        await _mediator.Send(new CancelOrderCommand { OrderId = orderId, Reason = $"Inventory insufficient: {reason}" }, ct);
    }

    // ── Compensation: the shipment could not be created → refund (if paid) + cancel order (P1-5) ──
    public async Task HandleShipmentCreationFailedAsync(Guid orderId, string reason, CancellationToken ct)
    {
        var saga = await _uow.Sagas.GetByOrderIdAsync(orderId, ct);
        if (saga is null) return;

        if (saga.CurrentStep != SagaStep.ShippingPending)
        {
            _logger.LogInformation("Saga {SagaId} not awaiting a shipment (at {Step}) — ignoring ShipmentCreationFailed", saga.Id, saga.CurrentStep);
            return;
        }

        await CompensateAsync(saga, $"Shipment could not be created: {reason}", ct);
    }

    // ── A delivery attempt failed after the shipment exists: recorded, the order is not cancelled (P1-5) ──
    public async Task HandleDeliveryFailedAsync(Guid orderId, string reason, int attempts, CancellationToken ct)
    {
        var saga = await _uow.Sagas.GetByOrderIdAsync(orderId, ct);
        if (saga is null) return;

        saga.RecordProblem($"Delivery failed (attempt {attempts}): {reason}");
        await _uow.Sagas.UpdateAsync(saga, ct);
        await _uow.SaveChangesAsync(ct);
        _logger.LogWarning("Saga {SagaId}: delivery of Order {OrderId} failed (attempt {Attempts}) — {Reason}", saga.Id, orderId, attempts, reason);
    }

    // ── The carrier confirmed the delivery → the order is Delivered (P1-5) ──
    /// <summary>Marks a Shipped order Delivered. Any other status (already Delivered, cancelled, partly shipped — other shipments are still on the way) is left alone.</summary>
    public async Task<bool> HandleDeliveryConfirmedAsync(Guid orderId, CancellationToken ct)
    {
        var order = await _uow.Orders.GetByIdAsync(orderId, ct);
        if (order is null)
        {
            _logger.LogWarning("DeliveryConfirmed for unknown Order {OrderId} ignored", orderId);
            return false;
        }

        if (order.Status != ERP.SO.Domain.Entities.OrderStatus.Shipped)
        {
            _logger.LogInformation("DeliveryConfirmed for Order {OrderId} at {Status} — not changed", orderId, order.Status);
            return false;
        }

        await _mediator.Send(new MarkAsDeliveredCommand { OrderId = orderId, Reason = "Delivered (carrier confirmation)" }, ct);
        return true;
    }

    // ── Timeouts: publish the step's command again, then compensate (P1-5) ──
    /// <summary>
    /// Looks at one Running saga: when its current step has been waiting longer than the step's timeout (or <paramref name="force"/> is set by a person) the command of that step is
    /// published again; once <see cref="SagaOptions.MaxRetries"/> retries are used the saga is compensated. A step without a timeout (approval) is never touched.
    /// </summary>
    public async Task<SagaTimeoutOutcome> HandleStepTimeoutAsync(Guid sagaId, SagaOptions options, DateTime nowUtc, bool force, CancellationToken ct)
    {
        var saga = await _uow.Sagas.GetByIdAsync(sagaId, ct);
        if (saga is null || saga.Status != SagaStatus.Running)
            return SagaTimeoutOutcome.NotDue;

        var timeout = options.TimeoutFor(saga.CurrentStep);
        if (timeout is null)
            return SagaTimeoutOutcome.NotDue;
        if (!force && nowUtc - saga.LastUpdatedAt < timeout.Value)
            return SagaTimeoutOutcome.NotDue;

        if (!force && saga.RetryCount >= options.MaxRetries)
        {
            _logger.LogWarning("Saga {SagaId} for Order {OrderId}: {Step} timed out after {Retries} retries — compensating", saga.Id, saga.OrderId, saga.CurrentStep, saga.RetryCount);
            await CompensateAsync(saga, $"Timed out in step {saga.CurrentStep} after {saga.RetryCount} retries", ct);
            return SagaTimeoutOutcome.TimedOut;
        }

        var step = saga.CurrentStep;
        saga.IncrementRetry();
        await _uow.Sagas.UpdateAsync(saga, ct);
        await _uow.SaveChangesAsync(ct);
        _logger.LogWarning("Saga {SagaId} for Order {OrderId}: {Step} waited too long — publishing its command again (retry {Retry})", saga.Id, saga.OrderId, step, saga.RetryCount);

        var order = await _uow.Orders.GetByIdAsync(saga.OrderId, ct);
        if (order is null)
        {
            _logger.LogError("Saga {SagaId}: order {OrderId} not found — nothing to publish again", saga.Id, saga.OrderId);
            return SagaTimeoutOutcome.Retried;
        }

        switch (step)
        {
            case SagaStep.PaymentPending:
                await _messageBus.PublishAsync(BuildInitiatePayment(saga.OrderId, saga.TenantId, order.TotalAmount, order.CustomerId, order.OrderNo, order.PaymentMethod), ct);
                break;
            case SagaStep.InventoryPending:
                await _messageBus.PublishAsync(new ReserveInventoryCommand
                {
                    OrderId = saga.OrderId,
                    TenantId = saga.TenantId,
                    Items = order.LineItems.Select(li => new SagaLineItem { ProductId = li.ProductId, Quantity = li.Quantity }).ToList()
                }, ct);
                break;
            case SagaStep.ShippingPending:
                await PublishCreateShipmentCommandAsync(saga, ct);
                break;
        }
        return SagaTimeoutOutcome.Retried;
    }

    /// <summary>Fails the saga, refunds the payment when there is one and cancels the order (the cancellation releases the reservation).</summary>
    private async Task CompensateAsync(OrderFulfillmentSagaState saga, string reason, CancellationToken ct)
    {
        _logger.LogWarning("Saga {SagaId}: compensating Order {OrderId} — {Reason}", saga.Id, saga.OrderId, reason);

        saga.BeginCompensation(reason);
        saga.MarkFailed(reason);
        await _uow.Sagas.UpdateAsync(saga, ct);
        await _uow.SaveChangesAsync(ct);

        await RefundOrCancelPaymentAsync(saga, $"Order cancelled: {reason}", ct);

        await _mediator.Send(new CancelOrderCommand { OrderId = saga.OrderId, Reason = reason }, ct);
    }
}

// ── Cross-service command DTOs published by the saga via IMessageBus ───────
// (Cancel stays in-process via SO's own real CancelOrderCommand — see above.)

public record InitiatePaymentCommand
{
    public Guid OrderId { get; init; }
    public Guid TenantId { get; init; }
    public decimal Amount { get; init; }
    /// <summary>COD, VNPay, MoMo, ZaloPay or Unspecified / null (P1-10).</summary>
    public string? PaymentMethod { get; init; }
    public string? Currency { get; init; }
    public Guid? CustomerId { get; init; }
    public string? OrderNumber { get; init; }
}

public record ReserveInventoryCommand
{
    public Guid OrderId { get; init; }
    public Guid TenantId { get; init; }
    public List<SagaLineItem> Items { get; init; } = new();
}

public record SagaLineItem
{
    public Guid ProductId { get; init; }
    public int Quantity { get; init; }
}

public record CreateShipmentCommand
{
    public Guid OrderId { get; init; }
    public Guid TenantId { get; init; }
    public string RecipientName { get; init; } = string.Empty;
    public string? RecipientEmail { get; init; }
    public SagaShippingAddress? ShippingAddress { get; init; }
    public List<SagaShipmentLine> Items { get; init; } = new();
}

public record SagaShippingAddress
{
    public string Street { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
}

public record SagaShipmentLine
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
}

public record RefundPaymentCommand
{
    public Guid PaymentId { get; init; }
    public Guid OrderId { get; init; }
    public Guid TenantId { get; init; }
    public string Reason { get; init; } = string.Empty;
}
