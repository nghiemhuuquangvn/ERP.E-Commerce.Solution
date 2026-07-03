using MediatR;

namespace ERP.WH.Application.Commands.Stocktakes;

public class RecordStocktakeCountCommand : IRequest<StocktakeDto>
{
    public Guid StocktakeId { get; set; }
    public Guid ProductId { get; set; }
    public int CountedQuantity { get; set; }
}

public class RecordStocktakeCountCommandHandler : IRequestHandler<RecordStocktakeCountCommand, StocktakeDto>
{
    private readonly IStocktakeRepository _stocktakeRepository;

    public RecordStocktakeCountCommandHandler(IStocktakeRepository stocktakeRepository)
    {
        _stocktakeRepository = stocktakeRepository;
    }

    public async Task<StocktakeDto> Handle(RecordStocktakeCountCommand request, CancellationToken cancellationToken)
    {
        var stocktake = await _stocktakeRepository.GetByIdAsync(request.StocktakeId, cancellationToken)
            ?? throw new InvalidOperationException($"Stocktake {request.StocktakeId} not found");

        stocktake.RecordCount(request.ProductId, request.CountedQuantity);
        await _stocktakeRepository.UpdateAsync(stocktake, cancellationToken);

        return StartStocktakeCommandHandler.Map(stocktake);
    }
}

public class CancelStocktakeCommand : IRequest<StocktakeDto>
{
    public Guid StocktakeId { get; set; }
}

public class CancelStocktakeCommandHandler : IRequestHandler<CancelStocktakeCommand, StocktakeDto>
{
    private readonly IStocktakeRepository _stocktakeRepository;

    public CancelStocktakeCommandHandler(IStocktakeRepository stocktakeRepository)
    {
        _stocktakeRepository = stocktakeRepository;
    }

    public async Task<StocktakeDto> Handle(CancelStocktakeCommand request, CancellationToken cancellationToken)
    {
        var stocktake = await _stocktakeRepository.GetByIdAsync(request.StocktakeId, cancellationToken)
            ?? throw new InvalidOperationException($"Stocktake {request.StocktakeId} not found");

        stocktake.Cancel();
        await _stocktakeRepository.UpdateAsync(stocktake, cancellationToken);

        return StartStocktakeCommandHandler.Map(stocktake);
    }
}
