using ERP.PO.Domain.Entities;
using ERP.PO.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.PO.Application.Features.PurchaseOrders;

/// <summary>
/// What the purchase order list can be narrowed by (Admin web and mobile app). Applied inside the database query, before paging,
/// so the total always counts every match. <c>From</c> / <c>To</c> are calendar days of <c>OrderDate</c>, both inclusive.
/// </summary>
public sealed record PurchaseOrderFilter(Guid? SupplierId, PurchaseOrderStatus? Status, DateOnly? From, DateOnly? To, string? Search)
{
    public IQueryable<PurchaseOrder> Apply(IQueryable<PurchaseOrder> orders)
    {
        if (From.HasValue && To.HasValue && From > To)
            throw new BusinessRuleException("Purchasing", "The start date is after the end date.");

        orders = orders.Where(po => !po.IsDeleted);
        if (SupplierId.HasValue) orders = orders.Where(po => po.SupplierId == SupplierId.Value);
        if (Status.HasValue) orders = orders.Where(po => po.Status == Status.Value);
        if (From.HasValue)
        {
            var from = From.Value.ToDateTime(TimeOnly.MinValue);
            orders = orders.Where(po => po.OrderDate >= from);
        }
        if (To.HasValue)
        {
            var before = To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            orders = orders.Where(po => po.OrderDate < before);
        }
        var text = Search?.Trim().ToLower();
        if (!string.IsNullOrEmpty(text))
            orders = orders.Where(po => po.PONumber.ToLower().Contains(text) || (po.Supplier != null && po.Supplier.Name.ToLower().Contains(text)));
        return orders;
    }

    /// <summary>
    /// The status as the clients write it: <c>SUBMITTED</c>, <c>Submitted</c>, <c>PENDING_FINANCE_APPROVAL</c> and
    /// <c>PendingFinanceApproval</c> all work. An unknown word is refused, never ignored (it used to return every order).
    /// </summary>
    public static PurchaseOrderStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        var key = status.Replace("_", "").Replace(" ", "");
        if (Enum.TryParse<PurchaseOrderStatus>(key, true, out var parsed) && Enum.IsDefined(parsed)) return parsed;
        throw new BusinessRuleException("Purchasing", $"Unknown purchase order status '{status}'.");
    }
}

/// <summary>Reads purchase orders for the list; implemented over the database in Infrastructure.</summary>
public interface IPurchaseOrderSearch
{
    Task<(IReadOnlyList<PurchaseOrder> Items, int Total)> SearchAsync(PurchaseOrderFilter filter, int pageNumber, int pageSize, CancellationToken cancellationToken);
}
