using MediatR;

namespace ERP.PO.Application.Features.PurchaseOrders.Queries;

/// <summary>Price comparison across suppliers for a given product code — previously entirely absent.</summary>
public class ComparePricesForProductQuery : IRequest<List<SupplierPriceComparisonDto>>
{
    public string ProductCode { get; set; } = string.Empty;
}

public class SupplierPriceComparisonDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal LatestUnitPrice { get; set; }
    public decimal AverageUnitPrice { get; set; }
    public DateTime LatestOrderDate { get; set; }
    public int OrderCount { get; set; }
}
