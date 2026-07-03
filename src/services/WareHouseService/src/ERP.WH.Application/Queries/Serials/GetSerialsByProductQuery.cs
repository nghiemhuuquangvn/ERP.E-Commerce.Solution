using MediatR;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Domain.Aggregates.Inventory;

namespace ERP.WH.Application.Queries.Serials;

public class SerialDto
{
    public Guid SerialId { get; set; }
    public Guid ProductId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public Guid? LotId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public DateTime? SoldDate { get; set; }
}

public class GetSerialsByProductQuery : IRequest<List<SerialDto>>
{
    public Guid ProductId { get; set; }
    public SerialStatus? Status { get; set; }
}

public class GetSerialsByProductQueryHandler : IRequestHandler<GetSerialsByProductQuery, List<SerialDto>>
{
    private readonly IInventorySerialRepository _serialRepository;

    public GetSerialsByProductQueryHandler(IInventorySerialRepository serialRepository)
    {
        _serialRepository = serialRepository;
    }

    public async Task<List<SerialDto>> Handle(GetSerialsByProductQuery request, CancellationToken cancellationToken)
    {
        var serials = await _serialRepository.GetByProductIdAsync(request.ProductId, request.Status, cancellationToken);

        return serials.Select(s => new SerialDto
        {
            SerialId = s.SerialId,
            ProductId = s.ProductId,
            SerialNumber = s.SerialNumber,
            LotId = s.LotId,
            Status = s.Status.ToString(),
            ReceivedDate = s.ReceivedDate,
            SoldDate = s.SoldDate
        }).ToList();
    }
}
