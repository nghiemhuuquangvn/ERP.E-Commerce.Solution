using ERP.MFG.Domain.Common;

namespace ERP.MFG.Domain.Engineering;

/// <summary>The state machine of a production order (BA spec 01 §4.2, BR-PO-02). Pure: no I/O.</summary>
public static class OrderStateMachine
{
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Allowed = new Dictionary<OrderStatus, OrderStatus[]>
    {
        [OrderStatus.Draft] = [OrderStatus.Released, OrderStatus.Cancelled],
        [OrderStatus.Released] = [OrderStatus.Draft, OrderStatus.Cancelled, OrderStatus.InProgress, OrderStatus.OnHold],
        [OrderStatus.InProgress] = [OrderStatus.OnHold, OrderStatus.Completed],
        [OrderStatus.OnHold] = [OrderStatus.Released, OrderStatus.InProgress],     // only back to the status held from (checked by the order)
        [OrderStatus.Completed] = [OrderStatus.InProgress, OrderStatus.Closed],
        [OrderStatus.Closed] = [],
        [OrderStatus.Cancelled] = [],
    };

    public static IReadOnlyList<OrderStatus> NextOf(OrderStatus from) => Allowed[from];

    public static bool CanTransition(OrderStatus from, OrderStatus to) => Allowed.TryGetValue(from, out var next) && next.Contains(to);

    /// <summary>A closed order is read only (<c>MFG_PO_014</c>); any other illegal pair is <c>MFG_PO_002</c> naming both statuses.</summary>
    public static void EnsureTransition(OrderStatus from, OrderStatus to)
    {
        if (from == OrderStatus.Closed) throw MfgRules.Fail(MfgErrors.Po014);
        if (!CanTransition(from, to)) throw MfgRules.Fail(MfgErrors.Po002, ("from", from), ("to", to));
    }
}

/// <summary>The state machine of an operation of an order (BA spec 01 §4.3). Pure: no I/O.</summary>
public static class OperationStateMachine
{
    private static readonly IReadOnlyDictionary<OperationStatus, OperationStatus[]> Allowed = new Dictionary<OperationStatus, OperationStatus[]>
    {
        [OperationStatus.Pending] = [OperationStatus.Ready],
        [OperationStatus.Ready] = [OperationStatus.Running, OperationStatus.Completed],
        [OperationStatus.Running] = [OperationStatus.Paused, OperationStatus.Ready, OperationStatus.Completed],   // Stop closes the time log and goes back to Ready
        [OperationStatus.Paused] = [OperationStatus.Running],
        [OperationStatus.Completed] = [OperationStatus.Ready],                                                      // Reopen (supervisor)
    };

    public static bool CanTransition(OperationStatus from, OperationStatus to) => Allowed.TryGetValue(from, out var next) && next.Contains(to);

    public static void EnsureTransition(OperationStatus from, OperationStatus to)
        => MfgRules.ThrowIf(!CanTransition(from, to), MfgErrors.Po002, ("from", from), ("to", to));
}
