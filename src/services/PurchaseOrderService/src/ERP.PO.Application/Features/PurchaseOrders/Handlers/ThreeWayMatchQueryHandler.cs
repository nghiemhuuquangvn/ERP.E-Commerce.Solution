using MediatR;
using ERP.PO.Application.Features.PurchaseOrders.Queries;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

public class ThreeWayMatchQueryHandler : IRequestHandler<ThreeWayMatchQuery, ThreeWayMatchResultDto>
{
    private readonly IRepository<PurchaseOrder> _repository;
    private readonly IUserContextService _userContextService;

    public ThreeWayMatchQueryHandler(IRepository<PurchaseOrder> repository, IUserContextService userContextService)
    {
        _repository = repository;
        _userContextService = userContextService;
    }

    public async Task<ThreeWayMatchResultDto> Handle(ThreeWayMatchQuery request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var order = await _repository.GetByIdAsync(request.PurchaseOrderId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PurchaseOrderId} not found");

        var result = new ThreeWayMatchResultDto { PurchaseOrderId = order.Id };

        foreach (var invoiceLine in request.InvoiceLines)
        {
            var poItem = order.Items.FirstOrDefault(i =>
                i.ProductCode.Equals(invoiceLine.ProductCode, StringComparison.OrdinalIgnoreCase));

            if (poItem == null)
            {
                result.Lines.Add(new ThreeWayMatchLineResult
                {
                    ProductCode = invoiceLine.ProductCode,
                    InvoicedQuantity = invoiceLine.InvoicedQuantity,
                    InvoicedUnitPrice = invoiceLine.InvoicedUnitPrice,
                    Status = "NotOnPurchaseOrder"
                });
                continue;
            }

            var quantityMatches = poItem.ReceivedQuantity == invoiceLine.InvoicedQuantity;
            var priceMatches = poItem.UnitPrice.Amount == invoiceLine.InvoicedUnitPrice;

            string status;
            if (poItem.ReceivedQuantity == 0)
                status = "NotYetReceived";
            else if (!quantityMatches)
                status = "QuantityMismatch";
            else if (!priceMatches)
                status = "PriceMismatch";
            else
                status = "Matched";

            result.Lines.Add(new ThreeWayMatchLineResult
            {
                ProductCode = poItem.ProductCode,
                OrderedQuantity = poItem.Quantity,
                ReceivedQuantity = poItem.ReceivedQuantity,
                InvoicedQuantity = invoiceLine.InvoicedQuantity,
                PurchaseOrderUnitPrice = poItem.UnitPrice.Amount,
                InvoicedUnitPrice = invoiceLine.InvoicedUnitPrice,
                QuantityMatches = quantityMatches,
                PriceMatches = priceMatches,
                Status = status
            });
        }

        result.IsFullyMatched = result.Lines.Count > 0 && result.Lines.All(l => l.Status == "Matched");
        return result;
    }
}
