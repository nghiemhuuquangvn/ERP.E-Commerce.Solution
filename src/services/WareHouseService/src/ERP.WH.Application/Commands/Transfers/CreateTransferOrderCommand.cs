using MediatR;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Transfers;

namespace ERP.WH.Application.Commands.Transfers;

public class TransferLineInput
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}

public class CreateTransferOrderCommand : IRequest<TransferOrderDto>
{
    public Guid FromLocationId { get; set; }
    public Guid ToLocationId { get; set; }
    public List<TransferLineInput> Lines { get; set; } = new();
    public string? Notes { get; set; }
}

public class TransferOrderLineDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}

public class TransferOrderDto
{
    public Guid TransferOrderId { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public Guid FromLocationId { get; set; }
    public Guid ToLocationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<TransferOrderLineDto> Lines { get; set; } = new();
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class CreateTransferOrderCommandHandler : IRequestHandler<CreateTransferOrderCommand, TransferOrderDto>
{
    private readonly IWarehouseUnitOfWork _uow;

    public CreateTransferOrderCommandHandler(IWarehouseUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<TransferOrderDto> Handle(CreateTransferOrderCommand request, CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0)
            throw new InvalidOperationException("A transfer order requires at least one line");

        // Availability check against the source location — previously missing entirely.
        // Only enforced when bin-level stock data actually exists for this product at
        // this location (additive tracking, not backfilled for old data); otherwise falls
        // back to the product-level rollup so this doesn't break transfers for products
        // that have never been received against a specific location.
        foreach (var line in request.Lines)
        {
            var locationStock = await _uow.InventoryLocationStocks.GetAsync(line.ProductId, request.FromLocationId, cancellationToken);
            if (locationStock != null)
            {
                if (locationStock.AvailableQuantity < line.Quantity)
                    throw new InvalidOperationException(
                        $"Insufficient stock for product {line.ProductId} at the source location. Available: {locationStock.AvailableQuantity}, Requested: {line.Quantity}");
                continue;
            }

            var inventory = await _uow.Inventories.GetByProductIdAsync(line.ProductId, cancellationToken);
            if (inventory != null && inventory.AvailableQuantity < line.Quantity)
                throw new InvalidOperationException(
                    $"Insufficient stock for product {line.ProductId}. Available: {inventory.AvailableQuantity}, Requested: {line.Quantity}");
        }

        var transferNumber = $"TO-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var transferOrder = new TransferOrder(transferNumber, request.FromLocationId, request.ToLocationId, request.Notes);

        foreach (var line in request.Lines)
            transferOrder.AddLine(line.ProductId, line.Quantity);

        await _uow.TransferOrders.AddAsync(transferOrder, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Map(transferOrder);
    }

    internal static TransferOrderDto Map(TransferOrder order) => new()
    {
        TransferOrderId = order.TransferOrderId,
        TransferNumber = order.TransferNumber,
        FromLocationId = order.FromLocationId,
        ToLocationId = order.ToLocationId,
        Status = order.Status.ToString(),
        Lines = order.Lines.Select(l => new TransferOrderLineDto { ProductId = l.ProductId, Quantity = l.Quantity }).ToList(),
        Notes = order.Notes,
        CreatedAt = order.CreatedAt,
        ShippedAt = order.ShippedAt,
        CompletedAt = order.CompletedAt
    };
}

public interface ITransferOrderRepository
{
    Task<TransferOrder?> GetByIdAsync(Guid transferOrderId, CancellationToken cancellationToken);
    Task<List<TransferOrder>> GetAllAsync(CancellationToken cancellationToken);
    Task AddAsync(TransferOrder transferOrder, CancellationToken cancellationToken);
    Task UpdateAsync(TransferOrder transferOrder, CancellationToken cancellationToken);
}
