using MediatR;
using ERP.Shared.Infrastructure.Caching;
using ERP.WH.Application.Interfaces;

namespace ERP.WH.Application.Commands.Inventory;

/// <summary>Sets the replenishment policy of one product: order when available stock (plus goods on order) is at or below Min, up to Max.</summary>
public class UpdateStockPolicyCommand : IRequest<StockPolicyResponse>
{
    public Guid ProductId { get; set; }
    public int MinStock { get; set; }
    public int MaxStock { get; set; }
    public int SafetyStock { get; set; }
}

public class StockPolicyResponse
{
    public Guid ProductId { get; set; }
    public int MinStock { get; set; }
    public int MaxStock { get; set; }
    public int SafetyStock { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsLowStock { get; set; }
}

public class UpdateStockPolicyCommandHandler : IRequestHandler<UpdateStockPolicyCommand, StockPolicyResponse>
{
    private readonly IWarehouseUnitOfWork _uow;
    private readonly IEventOutboxWriter _outboxWriter;
    private readonly ICacheService _cache;

    public UpdateStockPolicyCommandHandler(IWarehouseUnitOfWork uow, IEventOutboxWriter outboxWriter, ICacheService cache)
    {
        _uow = uow;
        _outboxWriter = outboxWriter;
        _cache = cache;
    }

    public async Task<StockPolicyResponse> Handle(UpdateStockPolicyCommand request, CancellationToken cancellationToken)
    {
        var inventory = await _uow.Inventories.GetByProductIdAsync(request.ProductId, cancellationToken)
            ?? throw new InvalidOperationException($"Product {request.ProductId} not found");

        try
        {
            inventory.SetStockPolicy(request.MinStock, request.MaxStock, request.SafetyStock);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            // Refusals are 400, not 500: report the rule, not the parameter plumbing.
            throw new InvalidOperationException(ex.Message.Split(" (Parameter")[0]);
        }

        // A higher minimum can put the product below it at once; a lower one lifts the "already alerted" mark.
        if (inventory.EvaluateLowStockAlert(DateTime.UtcNow) is { } alert)
            _outboxWriter.Stage(alert);

        await _uow.Inventories.UpdateAsync(inventory, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync($"wh:inventory:{request.ProductId}", cancellationToken);

        return new StockPolicyResponse
        {
            ProductId = inventory.ProductId,
            MinStock = inventory.MinStock,
            MaxStock = inventory.MaxStock,
            SafetyStock = inventory.SafetyStock,
            AvailableQuantity = inventory.AvailableQuantity,
            IsLowStock = inventory.IsLowStock()
        };
    }
}
