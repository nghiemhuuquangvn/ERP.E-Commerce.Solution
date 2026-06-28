using ERP.SO.Domain.Sagas;
using Microsoft.Extensions.Logging;

namespace ERP.SO.Application.Sagas;

/// <summary>
/// Orchestrates the Order Fulfillment Saga (choreography-based tracking).
///
/// Flow: Order Created → Payment Initiated → Inventory Reserved → Shipment Created → Completed
/// Compensation: any failure triggers rollback of completed steps.
/// </summary>
public sealed class OrderFulfillmentSaga
{
    private readonly IOrderFulfillmentSagaRepository _repository;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<OrderFulfillmentSaga> _logger;

    public OrderFulfillmentSaga(
        IOrderFulfillmentSagaRepository repository,
        IMessageBus messageBus,
        ILogger<OrderFulfillmentSaga> logger)
    {
        _repository = repository;
        _messageBus = messageBus;
        _logger = logger;
    }

    // ── Step 1: Order created → start saga + request payment ──────────────
    public async Task HandleOrderCreatedAsync(Guid orderId, Guid tenantId, decimal totalAmount, CancellationToken ct)
    {
        var existing = await _repository.GetByOrderIdAsync(orderId, ct);
        if (existing is not null)
        {
            _logger.LogWarning("Saga already exists for Order {OrderId}", orderId);
            return;
        }

        var saga = OrderFulfillmentSagaState.Start(orderId, tenantId);
        await _repository.AddAsync(saga, ct);

        _logger.LogInformation("Saga {SagaId} started for Order {OrderId} — requesting payment (Amount={Amount})",
            saga.Id, orderId, totalAmount);

        await _messageBus.PublishAsync(new InitiatePaymentCommand
        {
            OrderId = orderId,
            TenantId = tenantId,
            Amount = totalAmount,
            SagaId = saga.Id
        }, ct);
    }

    // ── Step 2: Payment processed → reserve inventory ─────────────────────
    public async Task HandlePaymentProcessedAsync(Guid orderId, Guid paymentId, CancellationToken ct)
    {
        var saga = await _repository.GetByOrderIdAsync(orderId, ct);
        if (saga is null)
        {
            _logger.LogError("No saga found for Order {OrderId} on PaymentProcessed", orderId);
            return;
        }

        saga.PaymentInitiated(paymentId);
        await _repository.UpdateAsync(saga, ct);

        _logger.LogInformation("Saga {SagaId}: payment {PaymentId} confirmed — requesting inventory reservation",
            saga.Id, paymentId);

        await _messageBus.PublishAsync(new ReserveInventoryCommand
        {
            OrderId = orderId,
            TenantId = saga.TenantId,
            SagaId = saga.Id
        }, ct);
    }

    // ── Step 3: Inventory reserved → create shipment ──────────────────────
    public async Task HandleInventoryReservedAsync(Guid orderId, Guid reservationId, CancellationToken ct)
    {
        var saga = await _repository.GetByOrderIdAsync(orderId, ct);
        if (saga is null)
        {
            _logger.LogError("No saga found for Order {OrderId} on InventoryReserved", orderId);
            return;
        }

        saga.InventoryReserved(reservationId);
        await _repository.UpdateAsync(saga, ct);

        _logger.LogInformation("Saga {SagaId}: inventory reserved ({ReservationId}) — requesting shipment creation",
            saga.Id, reservationId);

        await _messageBus.PublishAsync(new CreateShipmentCommand
        {
            OrderId = orderId,
            TenantId = saga.TenantId,
            SagaId = saga.Id
        }, ct);
    }

    // ── Step 4: Shipment created → saga complete ──────────────────────────
    public async Task HandleShipmentCreatedAsync(Guid orderId, Guid shipmentId, CancellationToken ct)
    {
        var saga = await _repository.GetByOrderIdAsync(orderId, ct);
        if (saga is null)
        {
            _logger.LogError("No saga found for Order {OrderId} on ShipmentCreated", orderId);
            return;
        }

        saga.ShipmentCreated(shipmentId);
        await _repository.UpdateAsync(saga, ct);

        _logger.LogInformation("Saga {SagaId} completed for Order {OrderId} in {Duration}ms",
            saga.Id, orderId, (saga.CompletedAt!.Value - saga.CreatedAt).TotalMilliseconds);
    }

    // ── Compensation: payment failed → cancel order ───────────────────────
    public async Task HandlePaymentFailedAsync(Guid orderId, string reason, CancellationToken ct)
    {
        var saga = await _repository.GetByOrderIdAsync(orderId, ct);
        if (saga is null) return;

        _logger.LogWarning("Saga {SagaId}: payment failed for Order {OrderId} — {Reason}. Compensating.",
            saga.Id, orderId, reason);

        saga.BeginCompensation($"Payment failed: {reason}");
        await _repository.UpdateAsync(saga, ct);

        await _messageBus.PublishAsync(new CancelOrderCommand
        {
            OrderId = orderId,
            TenantId = saga.TenantId,
            Reason = $"Payment failed: {reason}"
        }, ct);

        saga.MarkFailed($"Payment failed: {reason}");
        await _repository.UpdateAsync(saga, ct);
    }

    // ── Compensation: inventory insufficient → refund + cancel ────────────
    public async Task HandleInventoryInsufficientAsync(Guid orderId, string reason, CancellationToken ct)
    {
        var saga = await _repository.GetByOrderIdAsync(orderId, ct);
        if (saga is null) return;

        _logger.LogWarning("Saga {SagaId}: inventory insufficient for Order {OrderId} — {Reason}. Compensating.",
            saga.Id, orderId, reason);

        saga.BeginCompensation($"Inventory insufficient: {reason}");
        await _repository.UpdateAsync(saga, ct);

        // Refund payment if one was made
        if (saga.PaymentId.HasValue)
        {
            await _messageBus.PublishAsync(new RefundPaymentCommand
            {
                PaymentId = saga.PaymentId.Value,
                OrderId = orderId,
                TenantId = saga.TenantId,
                Reason = "Order cancelled: inventory insufficient"
            }, ct);
        }

        await _messageBus.PublishAsync(new CancelOrderCommand
        {
            OrderId = orderId,
            TenantId = saga.TenantId,
            Reason = $"Inventory insufficient: {reason}"
        }, ct);

        saga.MarkFailed($"Inventory insufficient: {reason}");
        await _repository.UpdateAsync(saga, ct);
    }
}

// ── Command DTOs used by Saga to publish inter-service commands ────────────

public record InitiatePaymentCommand
{
    public Guid OrderId { get; init; }
    public Guid TenantId { get; init; }
    public decimal Amount { get; init; }
    public Guid SagaId { get; init; }
}

public record ReserveInventoryCommand
{
    public Guid OrderId { get; init; }
    public Guid TenantId { get; init; }
    public Guid SagaId { get; init; }
}

public record CreateShipmentCommand
{
    public Guid OrderId { get; init; }
    public Guid TenantId { get; init; }
    public Guid SagaId { get; init; }
}

public record CancelOrderCommand
{
    public Guid OrderId { get; init; }
    public Guid TenantId { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public record RefundPaymentCommand
{
    public Guid PaymentId { get; init; }
    public Guid OrderId { get; init; }
    public Guid TenantId { get; init; }
    public string Reason { get; init; } = string.Empty;
}
