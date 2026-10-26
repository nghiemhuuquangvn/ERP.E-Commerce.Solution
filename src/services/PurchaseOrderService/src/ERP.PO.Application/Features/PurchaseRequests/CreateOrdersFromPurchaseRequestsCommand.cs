using ERP.PO.Application.Procurement;
using ERP.PO.Domain.Entities;
using ERP.PO.Domain.ValueObjects;
using ERP.Shared.Authorization;
using MediatR;

namespace ERP.PO.Application.Features.PurchaseRequests;

/// <summary>
/// Turns open purchase requests into orders: requests whose product has a preferred supplier are grouped by supplier into one
/// DRAFT purchase order each (at the supplier item's price); the others go into one RFQ to the suppliers that can supply them
/// (every active supplier when none is known). Nothing is approved. A request already linked to an order or RFQ is skipped, so
/// running it twice orders nothing twice.
/// </summary>
public class CreateOrdersFromPurchaseRequestsCommand : IRequest<CreateOrdersResult>
{
    /// <summary>Null = every open request.</summary>
    public List<Guid>? RequestIds { get; set; }
}

public record CreatedPurchaseOrderDto(Guid Id, string PoNumber, Guid SupplierId, string SupplierName, int Lines, decimal Total, string Currency, List<Guid> RequestIds);
public record CreatedRfqDto(Guid Id, string RfqNumber, int Lines, int Suppliers, List<Guid> RequestIds);
public record SkippedRequestDto(Guid RequestId, string Reason);
public record CreateOrdersResult(List<CreatedPurchaseOrderDto> PurchaseOrders, CreatedRfqDto? Rfq, List<SkippedRequestDto> Skipped);

public class CreateOrdersFromPurchaseRequestsCommandHandler : IRequestHandler<CreateOrdersFromPurchaseRequestsCommand, CreateOrdersResult>
{
    public const string DefaultUnit = "pcs";

    private readonly IPurchaseRequestRepository _requests;
    private readonly IProcurementStore _store;
    private readonly IUserContextService _user;

    public CreateOrdersFromPurchaseRequestsCommandHandler(IPurchaseRequestRepository requests, IProcurementStore store, IUserContextService user)
    {
        _requests = requests;
        _store = store;
        _user = user;
    }

    public async Task<CreateOrdersResult> Handle(CreateOrdersFromPurchaseRequestsCommand command, CancellationToken ct)
    {
        var user = _user.GetCurrentUser();
        var tenantId = new Guid(user.TenantId);
        var ids = command.RequestIds?.Distinct().ToList();
        var open = await _requests.GetOpenByIdsAsync(ids, ct);
        var skipped = new List<SkippedRequestDto>();

        // A requested id that is not open any more (ordered, dismissed, unknown) is reported, not an error.
        if (ids != null)
            foreach (var missing in ids.Except(open.Select(r => r.Id)))
                skipped.Add(new SkippedRequestDto(missing, "Not an open request"));

        var preferredBySupplier = new Dictionary<Guid, List<(PurchaseRequest Request, SupplierItem Item)>>();
        var forQuotation = new List<PurchaseRequest>();
        foreach (var request in open)
        {
            if (request.SuggestedQuantity <= 0) { skipped.Add(new SkippedRequestDto(request.Id, "Nothing to order")); continue; }

            // Resolved again now: the preferred supplier may have changed since the alert.
            var item = await _requests.GetPreferredSupplierItemAsync(request.ProductId, ct);
            if (item == null) { forQuotation.Add(request); continue; }

            if (!preferredBySupplier.TryGetValue(item.SupplierId, out var group)) preferredBySupplier[item.SupplierId] = group = new();
            group.Add((request, item));
        }

        var orders = new List<CreatedPurchaseOrderDto>();
        foreach (var (supplierId, group) in preferredBySupplier)
        {
            var supplier = await _store.GetSupplierAsync(supplierId, ct);
            var currency = supplier?.CurrencyCode ?? "USD";
            var order = Rules.Run(() =>
            {
                var leadDays = group.Max(g => g.Item.LeadTimeDays);
                var po = PurchaseOrder.Create($"PO-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}", supplierId, tenantId,
                    DateTime.UtcNow.Date.AddDays(leadDays),
                    $"Replenishment of {group.Count} product(s) below minimum stock");
                po.CreatedBy = user.UserId;
                foreach (var (request, item) in group)
                    po.AddItem(PurchaseOrderItem.Create(po.Id, request.ProductCode, request.ProductName, request.SuggestedQuantity, DefaultUnit,
                        new Money(item.LastPurchasePrice, currency)));
                return po;
            });

            foreach (var (request, _) in group) request.LinkPurchaseOrder(order.Id);
            _store.Add(order);
            orders.Add(new CreatedPurchaseOrderDto(order.Id, order.PONumber, supplierId, supplier?.Name ?? supplierId.ToString(), group.Count,
                group.Sum(g => g.Request.SuggestedQuantity * g.Item.LastPurchasePrice), currency, group.Select(g => g.Request.Id).ToList()));
        }

        CreatedRfqDto? rfqDto = null;
        if (forQuotation.Count > 0)
        {
            var supplierIds = await _requests.GetSupplierIdsForProductsAsync(forQuotation.Select(r => r.ProductId).ToList(), ct);
            if (supplierIds.Count == 0) supplierIds = await _requests.GetActiveSupplierIdsAsync(ct);

            if (supplierIds.Count == 0)
            {
                skipped.AddRange(forQuotation.Select(r => new SkippedRequestDto(r.Id, "No preferred supplier and no active supplier to ask")));
            }
            else
            {
                var rfq = Rules.Run(() =>
                {
                    var r = RequestForQuotation.Create($"RFQ-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
                        $"Replenishment {DateTime.UtcNow:yyyy-MM-dd}", tenantId, null, "Created from purchase requests (no preferred supplier)");
                    r.CreatedBy = user.UserId;
                    foreach (var request in forQuotation) r.AddLine(request.ProductCode, request.ProductName, request.SuggestedQuantity, DefaultUnit);
                    foreach (var supplierId in supplierIds) r.Invite(supplierId);
                    return r;
                });

                foreach (var request in forQuotation) request.LinkRfq(rfq.Id);
                _store.Add(rfq);
                rfqDto = new CreatedRfqDto(rfq.Id, rfq.RfqNumber, forQuotation.Count, supplierIds.Count, forQuotation.Select(r => r.Id).ToList());
            }
        }

        if (orders.Count > 0 || rfqDto != null)
            await _store.SaveChangesAsync(ct);

        return new CreateOrdersResult(orders, rfqDto, skipped);
    }
}
