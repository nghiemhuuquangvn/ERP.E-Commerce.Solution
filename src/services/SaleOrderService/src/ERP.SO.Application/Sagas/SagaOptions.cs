using ERP.SO.Domain.Sagas;

namespace ERP.SO.Application.Sagas;

/// <summary>
/// Timeouts of the order fulfillment saga (configuration section <c>Saga</c>, ROADMAP P1-5). A Running saga that stays in a step longer than
/// that step's timeout has its command published again; after <see cref="MaxRetries"/> retries it is compensated (refund if paid, order cancelled).
/// <see cref="SagaStep.AwaitingApproval"/> has no timeout: it waits for a person.
/// </summary>
public class SagaOptions
{
    public const string SectionName = "Saga";

    public int PaymentPendingMinutes { get; set; } = 15;
    public int InventoryPendingMinutes { get; set; } = 10;
    public int ShippingPendingMinutes { get; set; } = 15;

    /// <summary>How many times a step's command is published again before the saga gives up.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>How often the recovery job looks for stuck sagas.</summary>
    public int CheckIntervalSeconds { get; set; } = 60;

    /// <summary>The timeout of a step; null for a step that never times out (waiting for a person, or finished).</summary>
    public TimeSpan? TimeoutFor(SagaStep step) => step switch
    {
        SagaStep.PaymentPending => TimeSpan.FromMinutes(Math.Max(PaymentPendingMinutes, 0)),
        SagaStep.InventoryPending => TimeSpan.FromMinutes(Math.Max(InventoryPendingMinutes, 0)),
        SagaStep.ShippingPending => TimeSpan.FromMinutes(Math.Max(ShippingPendingMinutes, 0)),
        _ => null,
    };

    /// <summary>The shortest step timeout: the recovery job only loads sagas older than this.</summary>
    public TimeSpan ShortestTimeout => new[] { PaymentPendingMinutes, InventoryPendingMinutes, ShippingPendingMinutes }
        .Select(m => TimeSpan.FromMinutes(Math.Max(m, 0))).Min();
}

public enum SagaTimeoutOutcome
{
    /// <summary>Nothing to do (not Running, a step without a timeout, or not due yet).</summary>
    NotDue,

    /// <summary>The command of the current step was published again.</summary>
    Retried,

    /// <summary>The retries are used up: the saga was compensated.</summary>
    TimedOut,
}
