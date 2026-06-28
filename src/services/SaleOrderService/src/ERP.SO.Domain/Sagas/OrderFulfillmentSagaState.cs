namespace ERP.SO.Domain.Sagas;

public enum SagaStatus
{
    Running = 0,
    Completed = 1,
    Compensating = 2,
    Failed = 3
}

public enum SagaStep
{
    PaymentPending,
    InventoryPending,
    ShippingPending,
    Completed,
    Compensating,
    Failed
}

public class OrderFulfillmentSagaState
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrderId { get; private set; }
    public SagaStep CurrentStep { get; private set; }
    public SagaStatus Status { get; private set; }
    public Guid? PaymentId { get; private set; }
    public Guid? ReservationId { get; private set; }
    public Guid? ShipmentId { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime LastUpdatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private OrderFulfillmentSagaState() { }

    public static OrderFulfillmentSagaState Start(Guid orderId, Guid tenantId)
    {
        return new OrderFulfillmentSagaState
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            CurrentStep = SagaStep.PaymentPending,
            Status = SagaStatus.Running,
            RetryCount = 0,
            LastUpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void PaymentInitiated(Guid paymentId)
    {
        PaymentId = paymentId;
        CurrentStep = SagaStep.InventoryPending;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void InventoryReserved(Guid reservationId)
    {
        ReservationId = reservationId;
        CurrentStep = SagaStep.ShippingPending;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void ShipmentCreated(Guid shipmentId)
    {
        ShipmentId = shipmentId;
        CurrentStep = SagaStep.Completed;
        Status = SagaStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void BeginCompensation(string reason)
    {
        CurrentStep = SagaStep.Compensating;
        Status = SagaStatus.Compensating;
        ErrorMessage = reason;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        CurrentStep = SagaStep.Failed;
        Status = SagaStatus.Failed;
        ErrorMessage = reason;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void IncrementRetry()
    {
        RetryCount++;
        LastUpdatedAt = DateTime.UtcNow;
    }
}
