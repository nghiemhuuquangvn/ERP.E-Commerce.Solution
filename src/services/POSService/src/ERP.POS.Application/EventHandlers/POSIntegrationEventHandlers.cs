using ERP.Shared.Core.Domain;
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
    private readonly IPosUnitOfWork _uow;

    public ProductCreatedForPosHandler(ILogger<ProductCreatedForPosHandler> logger, IPosUnitOfWork uow)
    {
        _logger = logger;
        _uow = uow;
    }

    public async Task HandleAsync(ProductCreatedIntegrationEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("[POS] ProductCreated: syncing product {ProductId} ({Name})", @event.ProductId, @event.Name);

        await _uow.ProductCache.UpsertAsync(new PosProductCache
        {
            ProductId = @event.ProductId,
            Name = @event.Name,
            Sku = @event.Sku,
            Price = @event.Price,
            IsActive = @event.IsActive,
            SyncedAt = DateTime.UtcNow
        }, ct);
        await _uow.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Handles ProductPriceChanged — updates local price cache so POS uses current prices.
/// </summary>
public class ProductPriceChangedForPosHandler
{
    private readonly ILogger<ProductPriceChangedForPosHandler> _logger;
    private readonly IPosUnitOfWork _uow;

    public ProductPriceChangedForPosHandler(ILogger<ProductPriceChangedForPosHandler> logger, IPosUnitOfWork uow)
    {
        _logger = logger;
        _uow = uow;
    }

    public async Task HandleAsync(ProductPriceChangedIntegrationEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("[POS] ProductPriceChanged: {ProductId} {Old} → {New}", @event.ProductId, @event.OldPrice, @event.NewPrice);
        await _uow.ProductCache.UpdatePriceAsync(@event.ProductId, @event.NewPrice, ct);
        await _uow.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Handles PromotionCreated — caches active promotions for offline discount calculation.
/// </summary>
public class PromotionCreatedForPosHandler
{
    private readonly ILogger<PromotionCreatedForPosHandler> _logger;
    private readonly IPosUnitOfWork _uow;

    public PromotionCreatedForPosHandler(ILogger<PromotionCreatedForPosHandler> logger, IPosUnitOfWork uow)
    {
        _logger = logger;
        _uow = uow;
    }

    public async Task HandleAsync(PromotionCreatedIntegrationEvent @event, CancellationToken ct = default)
    {
        _logger.LogInformation("[POS] PromotionCreated: caching promotion {PromotionId} ({Name})", @event.PromotionId, @event.Name);

        await _uow.PromotionCache.UpsertAsync(new PosPromotionCache
        {
            PromotionId = @event.PromotionId,
            Name = @event.Name,
            DiscountType = @event.DiscountType,
            DiscountValue = @event.DiscountValue,
            StartDate = @event.StartDate,
            EndDate = @event.EndDate
        }, ct);
        await _uow.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Handles PaymentProcessed from PaymentService — relevant to POS for click-and-collect
/// / online-order-paid-in-store scenarios, where a POS Transaction is opened using the
/// same Id as the originating SalesOrder so this event can correlate back to it. Not
/// every PaymentProcessed event will match an in-progress POS transaction (most online
/// orders never touch a POS terminal at all) — a miss is expected and not an error.
/// </summary>
public class PaymentProcessedForPosHandler
{
    private readonly ILogger<PaymentProcessedForPosHandler> _logger;
    private readonly IPosUnitOfWork _uow;

    public PaymentProcessedForPosHandler(ILogger<PaymentProcessedForPosHandler> logger, IPosUnitOfWork uow)
    {
        _logger = logger;
        _uow = uow;
    }

    public async Task HandleAsync(PaymentProcessedIntegrationEvent @event, CancellationToken ct = default)
    {
        var transaction = await _uow.Transactions.GetByIdAsync(@event.OrderId, ct);
        if (transaction == null)
        {
            _logger.LogInformation("[POS] PaymentProcessed for order {OrderId} has no matching POS transaction — not a POS-originated sale.", @event.OrderId);
            return;
        }

        transaction.ConfirmExternalPayment(@event.PaymentId);
        await _uow.Transactions.UpdateAsync(transaction, ct);
        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("[POS] PaymentProcessed confirmed against transaction {TransactionId}", transaction.Id);
    }
}

// ── Cache DTOs for POS local sync ──────────────────────────────────────────

public class PosProductCache : ITenantOwned
{
    public Guid ProductId { get; set; }
    /// <summary>Owning tenant — stamped by POSDbContext on insert, from the source event's TenantScope (EnforceTenantOnSave).</summary>
    public Guid TenantId { get; private set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public DateTime SyncedAt { get; set; }
}

public class PosPromotionCache : ITenantOwned
{
    public Guid PromotionId { get; set; }
    /// <summary>Owning tenant — stamped by POSDbContext on insert, from the source event's TenantScope (EnforceTenantOnSave).</summary>
    public Guid TenantId { get; private set; }
    public string Name { get; set; } = string.Empty;
    public int DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
