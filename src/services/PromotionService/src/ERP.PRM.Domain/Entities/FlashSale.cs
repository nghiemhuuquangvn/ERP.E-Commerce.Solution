using ERP.Shared.Core.Domain;
using ERP.PRM.Domain.Enums;
using ERP.PRM.Domain.Events;

namespace ERP.PRM.Domain.Entities;

public class FlashSale : AggregateRoot
{
    private readonly List<FlashSaleItem> _items = new();

    public string Name { get; private set; } = string.Empty;
    public new string? Description { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime EndTime { get; private set; }
    public FlashSaleStatus Status { get; private set; }
    public IReadOnlyCollection<FlashSaleItem> Items => _items.AsReadOnly();

    private FlashSale() { }

    public static FlashSale Create(string name, DateTime startTime, DateTime endTime, string? description = null)
    {
        if (endTime <= startTime)
            throw new InvalidOperationException("End time must be after start time.");

        return new FlashSale
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            StartTime = startTime,
            EndTime = endTime,
            Status = FlashSaleStatus.Scheduled,
            CreatedAt = DateTime.UtcNow
        };
    }

    public FlashSaleItem AddItem(Guid productId, string productName, decimal originalPrice, decimal salePrice, int allocatedQuantity)
    {
        if (Status != FlashSaleStatus.Scheduled)
            throw new InvalidOperationException("Items can only be added to scheduled flash sales.");

        if (allocatedQuantity <= 0)
            throw new InvalidOperationException("Allocated quantity must be positive.");

        if (salePrice >= originalPrice)
            throw new InvalidOperationException("Sale price must be less than original price.");

        var existing = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existing != null)
            throw new InvalidOperationException($"Product {productId} is already in this flash sale.");

        var item = FlashSaleItem.Create(Id, productId, productName, originalPrice, salePrice, allocatedQuantity);
        _items.Add(item);
        return item;
    }

    public void Start()
    {
        if (Status != FlashSaleStatus.Scheduled)
            throw new InvalidOperationException("Only scheduled flash sales can be started.");

        if (!_items.Any())
            throw new InvalidOperationException("Cannot start a flash sale with no items.");

        Status = FlashSaleStatus.Active;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new FlashSaleStartedEvent(Id, Name, StartTime, EndTime,
            _items.Select(i => i.ProductId).ToList()));
    }

    public void Complete()
    {
        if (Status != FlashSaleStatus.Active)
            throw new InvalidOperationException("Only active flash sales can be completed.");

        Status = FlashSaleStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == FlashSaleStatus.Completed)
            throw new InvalidOperationException("Cannot cancel a completed flash sale.");

        Status = FlashSaleStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool TryReserve(Guid productId, int quantity)
    {
        if (Status != FlashSaleStatus.Active) return false;
        if (DateTime.UtcNow < StartTime || DateTime.UtcNow > EndTime) return false;

        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item == null || item.RemainingQuantity < quantity) return false;

        item.Reserve(quantity);
        UpdatedAt = DateTime.UtcNow;
        return true;
    }
}

public class FlashSaleItem : BaseEntity
{
    public Guid FlashSaleId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public decimal OriginalPrice { get; private set; }
    public decimal SalePrice { get; private set; }
    public int AllocatedQuantity { get; private set; }
    public int SoldQuantity { get; private set; }
    public int RemainingQuantity => AllocatedQuantity - SoldQuantity;
    public decimal DiscountPercentage => OriginalPrice > 0
        ? Math.Round((OriginalPrice - SalePrice) / OriginalPrice * 100, 2)
        : 0;

    private FlashSaleItem() { }

    internal static FlashSaleItem Create(Guid flashSaleId, Guid productId, string productName,
        decimal originalPrice, decimal salePrice, int allocatedQuantity)
    {
        return new FlashSaleItem
        {
            Id = Guid.NewGuid(),
            FlashSaleId = flashSaleId,
            ProductId = productId,
            ProductName = productName,
            OriginalPrice = originalPrice,
            SalePrice = salePrice,
            AllocatedQuantity = allocatedQuantity,
            SoldQuantity = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    internal void Reserve(int quantity)
    {
        if (quantity > RemainingQuantity)
            throw new InvalidOperationException($"Insufficient flash sale stock. Remaining: {RemainingQuantity}");

        SoldQuantity += quantity;
        UpdatedAt = DateTime.UtcNow;
    }
}
