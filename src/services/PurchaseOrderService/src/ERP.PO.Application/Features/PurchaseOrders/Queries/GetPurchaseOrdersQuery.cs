using MediatR;
using ERP.PO.Application.DTOs;

namespace ERP.PO.Application.Features.PurchaseOrders.Queries;

public class GetPurchaseOrdersQuery : IRequest<PurchaseOrderPage>
{
    public Guid? SupplierId { get; set; }
    public string? Status { get; set; }
    /// <summary>First order day (inclusive).</summary>
    public DateOnly? DateFrom { get; set; }
    /// <summary>Last order day (inclusive).</summary>
    public DateOnly? DateTo { get; set; }
    /// <summary>Part of the PO number or the supplier name.</summary>
    public string? Search { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>One page of the list and how many orders match in all.</summary>
public class PurchaseOrderPage
{
    public IReadOnlyList<PurchaseOrderDto> Items { get; set; } = Array.Empty<PurchaseOrderDto>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}
