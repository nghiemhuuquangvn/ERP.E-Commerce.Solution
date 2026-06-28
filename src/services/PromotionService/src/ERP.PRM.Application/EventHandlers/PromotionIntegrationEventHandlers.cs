using ERP.PRM.Application.Commands;
using ERP.PRM.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.PRM.Application.EventHandlers;

// Integration event contracts
public record ProductCreatedIntegrationEvent(Guid ProductId, string Name, string Category, decimal Price);
public record SalesOrderCreatedIntegrationEvent(Guid OrderId, Guid CustomerId, decimal TotalAmount, List<OrderLineDto> Lines);
public record TransactionCreatedIntegrationEvent(Guid TransactionId, Guid CashierId, decimal TotalAmount);

public record OrderLineDto(Guid ProductId, int Quantity, decimal UnitPrice);

/// <summary>
/// Handles SalesOrderCreated — auto-earn loyalty points for customer after order placed.
/// </summary>
public class SalesOrderCreatedForPromotionHandler
{
    private readonly IMediator _mediator;
    private readonly ILogger<SalesOrderCreatedForPromotionHandler> _logger;

    public SalesOrderCreatedForPromotionHandler(IMediator mediator, ILogger<SalesOrderCreatedForPromotionHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task HandleAsync(SalesOrderCreatedIntegrationEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("[PROMOTION] SalesOrderCreated: earning loyalty for Customer {CustomerId}, Amount={Amount}",
            @event.CustomerId, @event.TotalAmount);

        await _mediator.Send(new EarnLoyaltyPointsCommand
        {
            UserId = @event.CustomerId,
            OrderId = @event.OrderId,
            OrderAmount = @event.TotalAmount,
            Source = "SalesOrder"
        }, ct);
    }
}

/// <summary>
/// Handles TransactionCreated from POS — earn loyalty points for retail purchases.
/// </summary>
public class TransactionCreatedForPromotionHandler
{
    private readonly IMediator _mediator;
    private readonly ILogger<TransactionCreatedForPromotionHandler> _logger;

    public TransactionCreatedForPromotionHandler(IMediator mediator, ILogger<TransactionCreatedForPromotionHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task HandleAsync(TransactionCreatedIntegrationEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("[PROMOTION] POS TransactionCreated: earning loyalty for Cashier={CashierId}, Amount={Amount}",
            @event.CashierId, @event.TotalAmount);

        await _mediator.Send(new EarnLoyaltyPointsCommand
        {
            UserId = @event.CashierId,
            OrderId = @event.TransactionId,
            OrderAmount = @event.TotalAmount,
            Source = "POSTransaction"
        }, ct);
    }
}

/// <summary>
/// Handles ProductCreated — caches product info for promotion eligibility rules.
/// </summary>
public class ProductCreatedForPromotionHandler
{
    private readonly IPromotionProductCacheRepository _cache;
    private readonly ILogger<ProductCreatedForPromotionHandler> _logger;

    public ProductCreatedForPromotionHandler(IPromotionProductCacheRepository cache, ILogger<ProductCreatedForPromotionHandler> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task HandleAsync(ProductCreatedIntegrationEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("[PROMOTION] ProductCreated: caching {ProductId} for promotion rules", @event.ProductId);
        await _cache.UpsertAsync(@event.ProductId, @event.Category, @event.Price, ct);
    }
}
