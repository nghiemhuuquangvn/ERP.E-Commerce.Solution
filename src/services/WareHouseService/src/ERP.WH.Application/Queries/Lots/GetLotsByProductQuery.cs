using MediatR;
using ERP.WH.Application.Commands.Inventory;

namespace ERP.WH.Application.Queries.Lots;

public class LotDto
{
    public Guid LotId { get; set; }
    public Guid ProductId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public int ReceivedQuantity { get; set; }
    public int RemainingQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime ReceivedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsExpired { get; set; }
}

public class GetLotsByProductQuery : IRequest<List<LotDto>>
{
    public Guid ProductId { get; set; }
    public bool IncludeDepleted { get; set; }
}

public class GetLotsByProductQueryHandler : IRequestHandler<GetLotsByProductQuery, List<LotDto>>
{
    private readonly IInventoryLotRepository _lotRepository;

    public GetLotsByProductQueryHandler(IInventoryLotRepository lotRepository)
    {
        _lotRepository = lotRepository;
    }

    public async Task<List<LotDto>> Handle(GetLotsByProductQuery request, CancellationToken cancellationToken)
    {
        var lots = await _lotRepository.GetByProductIdAsync(request.ProductId, request.IncludeDepleted, cancellationToken);

        return lots
            .OrderBy(l => l.ReceivedDate)
            .Select(l => new LotDto
            {
                LotId = l.LotId,
                ProductId = l.ProductId,
                LotNumber = l.LotNumber,
                ReceivedQuantity = l.ReceivedQuantity,
                RemainingQuantity = l.RemainingQuantity,
                UnitCost = l.UnitCost,
                ReceivedDate = l.ReceivedDate,
                ExpiryDate = l.ExpiryDate,
                IsExpired = l.IsExpired()
            }).ToList();
    }
}

public class GetExpiringLotsQuery : IRequest<List<LotDto>>
{
    public int WithinDays { get; set; } = 30;
}

public class GetExpiringLotsQueryHandler : IRequestHandler<GetExpiringLotsQuery, List<LotDto>>
{
    private readonly IInventoryLotRepository _lotRepository;

    public GetExpiringLotsQueryHandler(IInventoryLotRepository lotRepository)
    {
        _lotRepository = lotRepository;
    }

    public async Task<List<LotDto>> Handle(GetExpiringLotsQuery request, CancellationToken cancellationToken)
    {
        var threshold = DateTime.UtcNow.AddDays(request.WithinDays);
        var lots = await _lotRepository.GetExpiringAsync(threshold, cancellationToken);

        return lots
            .OrderBy(l => l.ExpiryDate)
            .Select(l => new LotDto
            {
                LotId = l.LotId,
                ProductId = l.ProductId,
                LotNumber = l.LotNumber,
                ReceivedQuantity = l.ReceivedQuantity,
                RemainingQuantity = l.RemainingQuantity,
                UnitCost = l.UnitCost,
                ReceivedDate = l.ReceivedDate,
                ExpiryDate = l.ExpiryDate,
                IsExpired = l.IsExpired()
            }).ToList();
    }
}
