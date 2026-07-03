using MediatR;
using ERP.WH.Application.Commands.Stocktakes;

namespace ERP.WH.Application.Queries.Stocktakes;

public class GetStocktakeByIdQuery : IRequest<StocktakeDto?>
{
    public Guid StocktakeId { get; set; }
}

public class GetStocktakeByIdQueryHandler : IRequestHandler<GetStocktakeByIdQuery, StocktakeDto?>
{
    private readonly IStocktakeRepository _stocktakeRepository;

    public GetStocktakeByIdQueryHandler(IStocktakeRepository stocktakeRepository)
    {
        _stocktakeRepository = stocktakeRepository;
    }

    public async Task<StocktakeDto?> Handle(GetStocktakeByIdQuery request, CancellationToken cancellationToken)
    {
        var stocktake = await _stocktakeRepository.GetByIdAsync(request.StocktakeId, cancellationToken);
        return stocktake == null ? null : StartStocktakeCommandHandler.Map(stocktake);
    }
}

public class ListStocktakesQuery : IRequest<List<StocktakeDto>>
{
}

public class ListStocktakesQueryHandler : IRequestHandler<ListStocktakesQuery, List<StocktakeDto>>
{
    private readonly IStocktakeRepository _stocktakeRepository;

    public ListStocktakesQueryHandler(IStocktakeRepository stocktakeRepository)
    {
        _stocktakeRepository = stocktakeRepository;
    }

    public async Task<List<StocktakeDto>> Handle(ListStocktakesQuery request, CancellationToken cancellationToken)
    {
        var stocktakes = await _stocktakeRepository.GetAllAsync(cancellationToken);
        return stocktakes.Select(StartStocktakeCommandHandler.Map).ToList();
    }
}
