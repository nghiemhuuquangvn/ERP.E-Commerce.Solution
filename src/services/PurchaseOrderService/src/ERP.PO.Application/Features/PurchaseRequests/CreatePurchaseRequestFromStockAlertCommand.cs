using MediatR;
using ERP.PO.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ERP.PO.Application.Features.PurchaseRequests;

/// <summary>
/// Consumer-invoked only (WarehouseEventRabbitMqConsumer) — never exposed via a controller. Decides, with what Purchasing
/// knows (goods already on order), whether the product still needs replenishing: the request is created, refreshed (never
/// duplicated) or, when open orders now cover the need, dismissed. A repeated alert for a product that is still low just
/// refreshes the open request's figures.
/// </summary>
public class CreatePurchaseRequestFromStockAlertCommand : IRequest<PurchaseRequestDto?>
{
    public Guid ProductId { get; set; }
    public required string ProductName { get; set; }
    /// <summary>Warehouse SKU = product code of purchase orders. Null from an older Warehouse.</summary>
    public string? ProductSku { get; set; }
    public int CurrentQuantity { get; set; }
    /// <summary>Total minus reserved and locked. Older senders leave it 0 together with MaxStock = 0.</summary>
    public int AvailableQuantity { get; set; }
    public int ReorderPoint { get; set; }
    public int ReorderQuantity { get; set; }
    public int MinStock { get; set; }
    public int MaxStock { get; set; }
    public int SafetyStock { get; set; }
}

public class CreatePurchaseRequestFromStockAlertCommandHandler : IRequestHandler<CreatePurchaseRequestFromStockAlertCommand, PurchaseRequestDto?>
{
    public const string CoveredNote = "Covered by stock or orders";

    private readonly IPurchaseRequestRepository _repository;
    private readonly ILogger<CreatePurchaseRequestFromStockAlertCommandHandler> _logger;

    public CreatePurchaseRequestFromStockAlertCommandHandler(
        IPurchaseRequestRepository repository,
        ILogger<CreatePurchaseRequestFromStockAlertCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<PurchaseRequestDto?> Handle(CreatePurchaseRequestFromStockAlertCommand request, CancellationToken cancellationToken)
    {
        var productCode = string.IsNullOrWhiteSpace(request.ProductSku) ? request.ProductId.ToString("N") : request.ProductSku!;
        var available = request.MaxStock > 0 ? request.AvailableQuantity : request.CurrentQuantity;
        // Without a SKU there is no way to find the product's orders: on-order stays 0 (DEFERRED R2).
        var onOrder = string.IsNullOrWhiteSpace(request.ProductSku) ? 0 : await _repository.GetOnOrderQuantityAsync(productCode, cancellationToken);

        var need = ReplenishmentCalculator.From(available, onOrder, request.MinStock, request.MaxStock, request.SafetyStock,
            request.ReorderPoint, request.ReorderQuantity);
        var existing = await _repository.GetActiveByProductIdAsync(request.ProductId, cancellationToken);

        if (!need.IsNeeded || need.SuggestedQuantity <= 0)
        {
            if (existing == null) return null;

            _logger.LogInformation("Purchase request {RequestId} for product {ProductId} dismissed: {Available} available + {OnOrder} on order is above the minimum {Min}.",
                existing.Id, request.ProductId, need.Available, need.OnOrder, need.Min);
            existing.Dismiss(CoveredNote);
            await _repository.UpdateAsync(existing, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            return PurchaseRequestDto.Map(existing);
        }

        var purchaseRequest = existing ?? PurchaseRequest.CreateFromReplenishment(request.ProductId, productCode, request.ProductName, need);
        if (existing != null) existing.Refresh(need);

        var preferred = await _repository.GetPreferredSupplierItemAsync(request.ProductId, cancellationToken);
        purchaseRequest.SetPreferredSupplier(preferred?.SupplierId, preferred?.LastPurchasePrice, preferred?.LeadTimeDays);

        if (existing == null) await _repository.AddAsync(purchaseRequest, cancellationToken);
        else await _repository.UpdateAsync(purchaseRequest, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return PurchaseRequestDto.Map(purchaseRequest);
    }
}
