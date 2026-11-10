namespace ERP.MFG.Domain.Engineering;

/// <summary>One material as the reservation planner sees it: what the order still needs, what the Warehouse can give, what the order already holds.</summary>
public sealed record ReservationNeed(int LineNo, Guid ComponentItemId, string ComponentCode, decimal Need, decimal Allocatable, decimal OwnHeld);

/// <summary>The outcome for one material: the quantity to hold and the part of the need that cannot be covered.</summary>
public sealed record ReservationTarget(int LineNo, Guid ComponentItemId, string ComponentCode, decimal Need, decimal Allocatable, decimal Target, decimal Shortage);

/// <summary>
/// BR-PO-08: a released order holds <c>min(need, available)</c> of every material. What the order already holds counts as available to it (releasing and
/// holding again must not shrink its own reservation). Pure: the only place these numbers are computed.
/// </summary>
public static class ReservationPlanner
{
    /// <summary>The need still open: required minus what is out on the shop floor and what backflush has consumed; never negative.</summary>
    public static decimal OpenNeed(decimal required, decimal issued, decimal returned, decimal consumed) => Math.Max(required - (issued - returned) - consumed, 0m);

    public static ReservationTarget Plan(ReservationNeed n)
    {
        var need = Math.Max(n.Need, 0m);
        var available = Math.Max(n.Allocatable, 0m) + Math.Max(n.OwnHeld, 0m);
        var target = Math.Min(need, available);
        return new ReservationTarget(n.LineNo, n.ComponentItemId, n.ComponentCode, need, available, target, need - target);
    }

    public static IReadOnlyList<ReservationTarget> Plan(IEnumerable<ReservationNeed> needs) => needs.Select(Plan).ToList();
}
