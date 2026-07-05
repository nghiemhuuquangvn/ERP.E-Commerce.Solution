using MediatR;
using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Domain.Aggregates.Stocktakes;

namespace ERP.WH.Application.Commands.Stocktakes;

public class StocktakeLineDto
{
    public Guid ProductId { get; set; }
    public int SystemQuantity { get; set; }
    public int? CountedQuantity { get; set; }
    public int Variance { get; set; }
}

public class StocktakeDto
{
    public Guid StocktakeId { get; set; }
    public string StocktakeNumber { get; set; } = string.Empty;
    public Guid? LocationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<StocktakeLineDto> Lines { get; set; } = new();
    public string? Notes { get; set; }
    public string StartedBy { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class StartStocktakeCommand : IRequest<StocktakeDto>
{
    public Guid? LocationId { get; set; }
    public List<Guid>? ProductIds { get; set; }
    public string StartedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

/// <summary>
/// Snapshots current system quantities into the stocktake so counters compare
/// against a fixed baseline rather than a moving target while counting is underway.
/// When ProductIds is omitted, every product currently in Inventory is snapshotted.
/// </summary>
public class StartStocktakeCommandHandler : IRequestHandler<StartStocktakeCommand, StocktakeDto>
{
    private readonly IWarehouseUnitOfWork _uow;

    public StartStocktakeCommandHandler(IWarehouseUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<StocktakeDto> Handle(StartStocktakeCommand request, CancellationToken cancellationToken)
    {
        var stocktakeNumber = $"ST-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var stocktake = new Stocktake(stocktakeNumber, request.StartedBy, request.LocationId, request.Notes);

        var allInventory = await _uow.Inventories.GetAllAsync(cancellationToken);
        var candidates = request.ProductIds is { Count: > 0 }
            ? allInventory.Where(i => request.ProductIds.Contains(i.ProductId))
            : allInventory.AsEnumerable();

        foreach (var inventory in candidates)
            stocktake.AddLine(inventory.ProductId, inventory.TotalQuantity);

        await _uow.Stocktakes.AddAsync(stocktake, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Map(stocktake);
    }

    internal static StocktakeDto Map(Stocktake stocktake) => new()
    {
        StocktakeId = stocktake.StocktakeId,
        StocktakeNumber = stocktake.StocktakeNumber,
        LocationId = stocktake.LocationId,
        Status = stocktake.Status.ToString(),
        Lines = stocktake.Lines.Select(l => new StocktakeLineDto
        {
            ProductId = l.ProductId,
            SystemQuantity = l.SystemQuantity,
            CountedQuantity = l.CountedQuantity,
            Variance = l.Variance
        }).ToList(),
        Notes = stocktake.Notes,
        StartedBy = stocktake.StartedBy,
        StartedAt = stocktake.StartedAt,
        CompletedAt = stocktake.CompletedAt
    };
}

public interface IStocktakeRepository
{
    Task<Stocktake?> GetByIdAsync(Guid stocktakeId, CancellationToken cancellationToken);
    Task<List<Stocktake>> GetAllAsync(CancellationToken cancellationToken);
    Task AddAsync(Stocktake stocktake, CancellationToken cancellationToken);
    Task UpdateAsync(Stocktake stocktake, CancellationToken cancellationToken);
}
