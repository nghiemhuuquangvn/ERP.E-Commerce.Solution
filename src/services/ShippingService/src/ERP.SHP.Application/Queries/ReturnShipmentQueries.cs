using ERP.SHP.Application.DTOs;
using ERP.SHP.Application.Handlers;
using ERP.SHP.Application.Interfaces;
using MediatR;

namespace ERP.SHP.Application.Queries;

public class GetReturnShipmentByIdQuery : IRequest<ReturnShipmentDto?>
{
    public Guid ReturnShipmentId { get; set; }
}

public class GetReturnShipmentByIdQueryHandler : IRequestHandler<GetReturnShipmentByIdQuery, ReturnShipmentDto?>
{
    private readonly IReturnShipmentRepository _returns;
    public GetReturnShipmentByIdQueryHandler(IReturnShipmentRepository returns) => _returns = returns;

    public async Task<ReturnShipmentDto?> Handle(GetReturnShipmentByIdQuery request, CancellationToken cancellationToken)
    {
        var returnShipment = await _returns.GetByIdAsync(request.ReturnShipmentId, cancellationToken);
        return returnShipment == null ? null : InitiateReturnCommandHandler.MapToDto(returnShipment);
    }
}

public class ListReturnsByShipmentQuery : IRequest<List<ReturnShipmentDto>>
{
    public Guid OriginalShipmentId { get; set; }
}

public class ListReturnsByShipmentQueryHandler : IRequestHandler<ListReturnsByShipmentQuery, List<ReturnShipmentDto>>
{
    private readonly IReturnShipmentRepository _returns;
    public ListReturnsByShipmentQueryHandler(IReturnShipmentRepository returns) => _returns = returns;

    public async Task<List<ReturnShipmentDto>> Handle(ListReturnsByShipmentQuery request, CancellationToken cancellationToken)
    {
        var returns = await _returns.GetByOriginalShipmentIdAsync(request.OriginalShipmentId, cancellationToken);
        return returns.Select(InitiateReturnCommandHandler.MapToDto).ToList();
    }
}
