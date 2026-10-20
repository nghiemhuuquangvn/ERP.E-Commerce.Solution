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

    /// <summary>Inventory reserved but the manual Approve action on the order hasn't
    /// happened yet — shipment creation waits here rather than firing automatically.</summary>
    AwaitingApproval,
    ShippingPending,
    Completed,
    Compensating,
    Failed
}

public class OrderFulfillmentSagaState : ERP.Shared.Core.Domain.ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrderId { get; private set; }
    public SagaStep CurrentStep { get; private set; }
    public SagaStatus Status { get; private set; }
    public Guid? PaymentId { get; private set; }
    public Guid? ReservationId { get; private set; }
    public Guid? ShipmentId { get; private set; }

    /// <summary>Set by the in-process ApproveOrderCommandHandler call — independent of
    /// CurrentStep because a human can approve the order before, during, or after
    /// inventory reservation finishes.</summary>
    public bool Approved { get; private set; }
    public bool InventoryReserved { get; private set; }

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

    /// <summary>Records that inventory is reserved. Does not by itself move to
    /// ShippingPending — call <see cref="TryAdvanceToShipping"/> after this (and after
    /// <see cref="MarkApproved"/>) since either can complete first.</summary>
    public void RecordInventoryReserved(Guid reservationId)
    {
        ReservationId = reservationId;
        InventoryReserved = true;
        CurrentStep = SagaStep.AwaitingApproval;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Idempotent — a redelivered/duplicate Approve signal is a no-op.</summary>
    public void MarkApproved()
    {
        if (Approved) return;
        Approved = true;
        LastUpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Only actually advances the step once both Approved and
    /// InventoryReserved are true; otherwise this is a no-op recording of whichever
    /// signal just arrived.</summary>
    public bool TryAdvanceToShipping()
    {
        if (!Approved || !InventoryReserved) return false;
        if (CurrentStep is SagaStep.ShippingPending or SagaStep.Completed) return false;

        CurrentStep = SagaStep.ShippingPending;
        LastUpdatedAt = DateTime.UtcNow;
        return true;
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
