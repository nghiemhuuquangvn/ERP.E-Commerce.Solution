using MediatR;
using ERP.WH.Application.Interfaces;

namespace ERP.WH.Application.Commands.Stocktakes;

public class RecordStocktakeCountCommand : IRequest<StocktakeDto>
{
    public Guid StocktakeId { get; set; }
    public Guid ProductId { get; set; }
    public int CountedQuantity { get; set; }
    public string? VarianceReason { get; set; }
}

public class RecordStocktakeCountCommandHandler : IRequestHandler<RecordStocktakeCountCommand, StocktakeDto>
{
    private readonly IWarehouseUnitOfWork _uow;

    public RecordStocktakeCountCommandHandler(IWarehouseUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<StocktakeDto> Handle(RecordStocktakeCountCommand request, CancellationToken cancellationToken)
    {
        var stocktake = await _uow.Stocktakes.GetByIdAsync(request.StocktakeId, cancellationToken)
            ?? throw new InvalidOperationException($"Stocktake {request.StocktakeId} not found");

        stocktake.RecordCount(request.ProductId, request.CountedQuantity, request.VarianceReason);
        await _uow.Stocktakes.UpdateAsync(stocktake, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return StartStocktakeCommandHandler.Map(stocktake);
    }
}

public class CancelStocktakeCommand : IRequest<StocktakeDto>
{
    public Guid StocktakeId { get; set; }
}

public class CancelStocktakeCommandHandler : IRequestHandler<CancelStocktakeCommand, StocktakeDto>
{
    private readonly IWarehouseUnitOfWork _uow;

    public CancelStocktakeCommandHandler(IWarehouseUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<StocktakeDto> Handle(CancelStocktakeCommand request, CancellationToken cancellationToken)
    {
        var stocktake = await _uow.Stocktakes.GetByIdAsync(request.StocktakeId, cancellationToken)
            ?? throw new InvalidOperationException($"Stocktake {request.StocktakeId} not found");

        stocktake.Cancel();
        await _uow.Stocktakes.UpdateAsync(stocktake, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return StartStocktakeCommandHandler.Map(stocktake);
    }
}
