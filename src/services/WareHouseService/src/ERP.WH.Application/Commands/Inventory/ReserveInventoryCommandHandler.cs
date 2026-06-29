using MediatR;
using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Domain.Events;
using InventoryEntity = ERP.WH.Domain.Aggregates.Inventory.Inventory;

namespace ERP.WH.Application.Commands.Inventory;

public class ReserveInventoryCommandHandler : IRequestHandler<ReserveInventoryCommand, ReserveInventoryResponse>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IReservationRepository _reservationRepository;
    private readonly IEventPublisher _eventPublisher;

    public ReserveInventoryCommandHandler(
        IInventoryRepository inventoryRepository,
        IReservationRepository reservationRepository,
        IEventPublisher eventPublisher)
    {
        _inventoryRepository = inventoryRepository;
        _reservationRepository = reservationRepository;
        _eventPublisher = eventPublisher;
    }

    public async Task<ReserveInventoryResponse> Handle(ReserveInventoryCommand request, CancellationToken cancellationToken)
    {
        var reservedItems = new List<ReservedItem>();
        var reservedItemResponses = new List<ReservedItemResponse>();

        foreach (var item in request.Items)
        {
            var inventory = await _inventoryRepository.GetByProductIdAsync(item.ProductId, cancellationToken);
            if (inventory == null)
                throw new InvalidOperationException($"Product {item.ProductId} not found in inventory");

            if (!inventory.CanReserve(item.Quantity))
                throw new InvalidOperationException(
                    $"Cannot reserve {item.Quantity} units of {inventory.ProductName}. Available: {inventory.AvailableQuantity}");

            inventory.ReserveStock(item.Quantity);
            await _inventoryRepository.UpdateAsync(inventory, cancellationToken);

            reservedItems.Add(new ReservedItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                WarehouseLocation = "DEFAULT_LOCATION"
            });

            reservedItemResponses.Add(new ReservedItemResponse
            {
                ProductId = item.ProductId,
                ReservedQuantity = item.Quantity,
                Location = "DEFAULT_LOCATION"
            });
        }

        var reservation = new Reservation(request.OrderId, reservedItems, request.HoldUntil);

        var eventItems = reservedItems.Select(i => new ReservedItemDto
        {
            ProductId = i.ProductId,
            Quantity = i.Quantity,
            WarehouseLocation = i.WarehouseLocation
        }).ToList();

        await _reservationRepository.AddAsync(reservation, cancellationToken);

        var @event = new InventoryReservedEvent
        {
            ReservationId = reservation.ReservationId,
            OrderId = request.OrderId,
            ReservedItems = eventItems,
            ReservedAt = DateTime.UtcNow,
            CorrelationId = request.CorrelationId,
            EventCreatedAt = DateTime.UtcNow
        };

        await _eventPublisher.PublishAsync(@event, cancellationToken);

        return new ReserveInventoryResponse
        {
            ReservationId = reservation.ReservationId,
            OrderId = request.OrderId,
            ReservedItems = reservedItemResponses,
            Status = "Reserved",
            ReservedAt = DateTime.UtcNow
        };
    }
}

public interface IInventoryRepository
{
    Task<InventoryEntity> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken);
    Task UpdateAsync(InventoryEntity inventory, CancellationToken cancellationToken);
    Task AddAsync(InventoryEntity inventory, CancellationToken cancellationToken);
    Task<List<InventoryEntity>> GetAllAsync(CancellationToken cancellationToken);
}

public interface IReservationRepository
{
    Task<Reservation> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken);
    Task<List<Reservation>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);
    Task AddAsync(Reservation reservation, CancellationToken cancellationToken);
    Task UpdateAsync(Reservation reservation, CancellationToken cancellationToken);
}

public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken);
}
