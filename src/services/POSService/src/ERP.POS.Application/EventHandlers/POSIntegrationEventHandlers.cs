using ERP.POS.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ERP.POS.Application.EventHandlers;

// Integration event contracts (consumed from other services via RabbitMQ/Kafka)
public record ProductCreatedIntegrationEvent(Guid ProductId, string Name, string Sku, decimal Price, bool IsActive);
public record ProductUpdatedIntegrationEvent(Guid ProductId, string Name, string Sku, decimal Price, bool IsActive);
public record ProductPriceChangedIntegrationEvent(Guid ProductId, decimal OldPrice, decimal NewPrice);
public record PromotionCreatedIntegrationEvent(Guid PromotionId, string Name, int DiscountType, decimal DiscountValue, DateTime StartDate, DateTime EndDate);
public record PaymentProcessedIntegrationEvent(Guid PaymentId, Guid OrderId, decimal Amount, string Method);

/// <summary>
/// Handles ProductCreated from ProductService — syncs product catalogue for offline POS use.
/// </summary>
public class ProductCreatedForPosHandler
{
    private readonly ILogger<ProductCreatedForPosHandler> _logger;
    private readonly IPosProductCacheRepository _cache;

    public ProductCreatedForPosHandler(ILogger<ProductCreatedForPosHandler> logger, IPosProductCacheRepository cache)
    {
        _logger = logger;
        _cache = cache;
    }

    public async Task HandleAsync(ProductCreatedIntegrationEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("[POS] ProductCreated: syncing product {ProductId} ({Name})", @event.ProductId, @event.Name);

        await _cache.UpsertAsync(new PosProductCache
        {
            ProductId = @event.ProductId,
            Name = @event.Name,
            Sku = @event.Sku,
            Price = @event.Price,
            IsActive = @event.IsActive,
            SyncedAt = DateTime.UtcNow
        }, ct);
    }
}

/// <summary>
/// Handles ProductPriceChanged — updates local price cache so POS uses current prices.
/// </summary>
public class ProductPriceChangedForPosHandler
{
    private readonly ILogger<ProductPriceChangedForPosHandler> _logger;
    private readonly IPosProductCacheRepository _cache;

    public ProductPriceChangedForPosHandler(ILogger<ProductPriceChangedForPosHandler> logger, IPosProductCacheRepository cache)
    {
        _logger = logger;
        _cache = cache;
    }

    public async Task HandleAsync(ProductPriceChangedIntegrationEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("[POS] ProductPriceChanged: {ProductId} {Old} → {New}", @event.ProductId, @event.OldPrice, @event.NewPrice);
        await _cache.UpdatePriceAsync(@event.ProductId, @event.NewPrice, ct);
    }
}

/// <summary>
/// Handles PromotionCreated — caches active promotions for offline discount calculation.
/// </summary>
public class PromotionCreatedForPosHandler
{
    private readonly ILogger<PromotionCreatedForPosHandler> _logger;
    private readonly IPosPromotionCacheRepository _cache;

    public PromotionCreatedForPosHandler(ILogger<PromotionCreatedForPosHandler> logger, IPosPromotionCacheRepository cache)
    {
        _logger = logger;
        _cache = cache;
    }

    public async Task HandleAsync(PromotionCreatedIntegrationEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("[POS] PromotionCreated: caching promotion {PromotionId} ({Name})", @event.PromotionId, @event.Name);

        await _cache.UpsertAsync(new PosPromotionCache
        {
            PromotionId = @event.PromotionId,
            Name = @event.Name,
            DiscountType = @event.DiscountType,
            DiscountValue = @event.DiscountValue,
            StartDate = @event.StartDate,
            EndDate = @event.EndDate
        }, ct);
    }
}

// ── Cache DTOs for POS local sync ──────────────────────────────────────────

public class PosProductCache
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public DateTime SyncedAt { get; set; }
}

public class PosPromotionCache
{
    public Guid PromotionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
