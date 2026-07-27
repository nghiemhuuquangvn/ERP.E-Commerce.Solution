using MediatR;
using ERP.PO.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ERP.PO.Application.Features.PurchaseRequests;

/// <summary>
/// Consumer-invoked only (WarehouseEventRabbitMqConsumer) — never exposed via a
/// controller. Idempotent against repeated StockAlertEvents for the same still-low
/// product: a second alert while a Suggested request is already open is a no-op rather
/// than spamming a new row every time the low-stock condition re-fires.
/// </summary>
public class CreatePurchaseRequestFromStockAlertCommand : IRequest<PurchaseRequestDto?>
{
    public Guid ProductId { get; set; }
    public required string ProductName { get; set; }
    public int CurrentQuantity { get; set; }
    public int ReorderPoint { get; set; }
    public int ReorderQuantity { get; set; }
}

public class CreatePurchaseRequestFromStockAlertCommandHandler : IRequestHandler<CreatePurchaseRequestFromStockAlertCommand, PurchaseRequestDto?>
{
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
        var existing = await _repository.GetActiveByProductIdAsync(request.ProductId, cancellationToken);
        if (existing != null)
        {
            _logger.LogInformation(
                "Skipping StockAlertEvent for product {ProductId} — an open purchase request {RequestId} already exists.",
                request.ProductId, existing.Id);
            return null;
        }

        var purchaseRequest = PurchaseRequest.Create(
            request.ProductId,
            request.ProductName,
            request.CurrentQuantity,
            request.ReorderPoint,
            request.ReorderQuantity);

        await _repository.AddAsync(purchaseRequest, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return PurchaseRequestDto.Map(purchaseRequest);
    }
}
