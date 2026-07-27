using MediatR;
using ERP.WH.Application.Commands.Warehouses;
using ERP.WH.Application.Interfaces;

namespace ERP.WH.Application.Queries.Warehouses;

public class GetWarehousesQuery : IRequest<List<WarehouseDto>>
{
}

public class GetWarehousesQueryHandler : IRequestHandler<GetWarehousesQuery, List<WarehouseDto>>
{
    private readonly IWarehouseUnitOfWork _uow;

    public GetWarehousesQueryHandler(IWarehouseUnitOfWork uow) => _uow = uow;

    public async Task<List<WarehouseDto>> Handle(GetWarehousesQuery request, CancellationToken cancellationToken)
    {
        var warehouses = await _uow.Warehouses.GetAllAsync(cancellationToken);
        return warehouses.Select(CreateWarehouseCommandHandler.Map).ToList();
    }
}

public class GetWarehouseByIdQuery : IRequest<WarehouseDto?>
{
    public Guid Id { get; set; }
}

public class GetWarehouseByIdQueryHandler : IRequestHandler<GetWarehouseByIdQuery, WarehouseDto?>
{
    private readonly IWarehouseUnitOfWork _uow;

    public GetWarehouseByIdQueryHandler(IWarehouseUnitOfWork uow) => _uow = uow;

    public async Task<WarehouseDto?> Handle(GetWarehouseByIdQuery request, CancellationToken cancellationToken)
    {
        var warehouse = await _uow.Warehouses.GetByIdAsync(request.Id, cancellationToken);
        return warehouse == null ? null : CreateWarehouseCommandHandler.Map(warehouse);
    }
}
