using MediatR;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Location;

namespace ERP.WH.Application.Commands.Warehouses;

public class WarehouseDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsActive { get; set; }
}

public class CreateWarehouseCommand : IRequest<WarehouseDto>
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
}

public class CreateWarehouseCommandHandler : IRequestHandler<CreateWarehouseCommand, WarehouseDto>
{
    private readonly IWarehouseUnitOfWork _uow;

    public CreateWarehouseCommandHandler(IWarehouseUnitOfWork uow) => _uow = uow;

    public async Task<WarehouseDto> Handle(CreateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = new Warehouse(request.Code, request.Name, request.Address);
        await _uow.Warehouses.AddAsync(warehouse, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Map(warehouse);
    }

    internal static WarehouseDto Map(Warehouse w) => new()
    {
        Id = w.Id,
        Code = w.Code,
        Name = w.Name,
        Address = w.Address,
        IsActive = w.IsActive
    };
}

public class UpdateWarehouseCommand : IRequest<WarehouseDto>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
}

public class UpdateWarehouseCommandHandler : IRequestHandler<UpdateWarehouseCommand, WarehouseDto>
{
    private readonly IWarehouseUnitOfWork _uow;

    public UpdateWarehouseCommandHandler(IWarehouseUnitOfWork uow) => _uow = uow;

    public async Task<WarehouseDto> Handle(UpdateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await _uow.Warehouses.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Warehouse {request.Id} not found");

        warehouse.Update(request.Name, request.Address);
        await _uow.Warehouses.UpdateAsync(warehouse, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return CreateWarehouseCommandHandler.Map(warehouse);
    }
}

public class SetWarehouseActiveCommand : IRequest<WarehouseDto>
{
    public Guid Id { get; set; }
    public bool IsActive { get; set; }
}

public class SetWarehouseActiveCommandHandler : IRequestHandler<SetWarehouseActiveCommand, WarehouseDto>
{
    private readonly IWarehouseUnitOfWork _uow;

    public SetWarehouseActiveCommandHandler(IWarehouseUnitOfWork uow) => _uow = uow;

    public async Task<WarehouseDto> Handle(SetWarehouseActiveCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await _uow.Warehouses.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Warehouse {request.Id} not found");

        if (request.IsActive) warehouse.Activate(); else warehouse.Deactivate();
        await _uow.Warehouses.UpdateAsync(warehouse, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return CreateWarehouseCommandHandler.Map(warehouse);
    }
}
