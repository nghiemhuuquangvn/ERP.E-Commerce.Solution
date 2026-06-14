using MediatR;

namespace ERP.WH.Application.EventHandlers;

public class PurchaseOrderCreatedEvent
{
    public Guid PurchaseOrderId { get; set; }
    public Guid SupplierId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public List<PurchaseLineItem> LineItems { get; set; }
    public DateTime ExpectedDeliveryDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CorrelationId { get; set; }
}

public class PurchaseLineItem
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class PurchaseOrderCreatedEventHandler : INotificationHandler<PurchaseOrderCreatedEvent>
{
    private readonly IReceiptRepository _receiptRepository;
    private readonly IInventoryRepository _inventoryRepository;

    public PurchaseOrderCreatedEventHandler(
        IReceiptRepository receiptRepository,
        IInventoryRepository inventoryRepository)
    {
        _receiptRepository = receiptRepository;
        _inventoryRepository = inventoryRepository;
    }

    public async Task Handle(PurchaseOrderCreatedEvent @event, CancellationToken cancellationToken)
    {
        foreach (var item in @event.LineItems)
        {
            var inventory = await _inventoryRepository.GetByProductIdAsync(item.ProductId, cancellationToken);

            if (inventory == null)
            {
                inventory = new Domain.Aggregates.Inventory.Inventory(
                    item.ProductId,
                    $"Product-{item.ProductId}");
                await _inventoryRepository.AddAsync(inventory, cancellationToken);
            }

            var receipt = new ReceiptSchedule
            {
                PurchaseOrderId = @event.PurchaseOrderId,
                ProductId = item.ProductId,
                ExpectedQuantity = item.Quantity,
                ExpectedDeliveryDate = @event.ExpectedDeliveryDate,
                CreatedAt = DateTime.UtcNow
            };

            await _receiptRepository.AddAsync(receipt, cancellationToken);
        }
    }
}

public class ReceiptSchedule
{
    public Guid PurchaseOrderId { get; set; }
    public Guid ProductId { get; set; }
    public int ExpectedQuantity { get; set; }
    public DateTime ExpectedDeliveryDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public interface IReceiptRepository
{
    Task AddAsync(ReceiptSchedule receipt, CancellationToken cancellationToken);
}
