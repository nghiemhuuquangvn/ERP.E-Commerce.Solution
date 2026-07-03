using MediatR;
using ERP.PO.Application.Features.PurchaseOrders.Queries;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

/// <summary>
/// Joins PurchaseOrderItems + PurchaseOrders + Suppliers in-memory (the shared
/// generic IRepository&lt;T&gt; has no cross-aggregate join/IQueryable escape hatch,
/// same constraint noted in GetPurchaseOrdersQueryHandler) to answer "who's cheapest
/// for this product, historically?" — the ❌ list's "Price comparison giữa các
/// supplier" item.
/// </summary>
public class ComparePricesForProductQueryHandler : IRequestHandler<ComparePricesForProductQuery, List<SupplierPriceComparisonDto>>
{
    private readonly IRepository<PurchaseOrder> _orderRepository;
    private readonly IRepository<PurchaseOrderItem> _itemRepository;
    private readonly IRepository<Supplier> _supplierRepository;
    private readonly IUserContextService _userContextService;

    public ComparePricesForProductQueryHandler(
        IRepository<PurchaseOrder> orderRepository,
        IRepository<PurchaseOrderItem> itemRepository,
        IRepository<Supplier> supplierRepository,
        IUserContextService userContextService)
    {
        _orderRepository = orderRepository;
        _itemRepository = itemRepository;
        _supplierRepository = supplierRepository;
        _userContextService = userContextService;
    }

    public async Task<List<SupplierPriceComparisonDto>> Handle(ComparePricesForProductQuery request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var items = (await _itemRepository.GetAllAsync(tenantId, cancellationToken))
            .Where(i => i.ProductCode.Equals(request.ProductCode, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (items.Count == 0)
            return new List<SupplierPriceComparisonDto>();

        var orders = (await _orderRepository.GetAllAsync(tenantId, cancellationToken))
            .ToDictionary(o => o.Id);

        var suppliers = (await _supplierRepository.GetAllAsync(tenantId, cancellationToken))
            .ToDictionary(s => s.Id);

        var lines = items
            .Where(i => orders.ContainsKey(i.PurchaseOrderId))
            .Select(i => new { Item = i, Order = orders[i.PurchaseOrderId] })
            .Where(x => suppliers.ContainsKey(x.Order.SupplierId));

        return lines
            .GroupBy(x => x.Order.SupplierId)
            .Select(g =>
            {
                var latest = g.OrderByDescending(x => x.Order.OrderDate).First();
                return new SupplierPriceComparisonDto
                {
                    SupplierId = g.Key,
                    SupplierName = suppliers[g.Key].Name,
                    LatestUnitPrice = latest.Item.UnitPrice.Amount,
                    AverageUnitPrice = Math.Round(g.Average(x => x.Item.UnitPrice.Amount), 2),
                    LatestOrderDate = latest.Order.OrderDate,
                    OrderCount = g.Select(x => x.Order.Id).Distinct().Count()
                };
            })
            .OrderBy(dto => dto.LatestUnitPrice)
            .ToList();
    }
}
