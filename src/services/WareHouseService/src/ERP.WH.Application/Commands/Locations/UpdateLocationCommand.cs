using MediatR;
using ERP.WH.Application.Interfaces;

namespace ERP.WH.Application.Commands.Locations;

public class UpdateLocationCommand : IRequest<LocationDto>
{
    public Guid LocationId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? Name { get; set; }
    public string? Zone { get; set; }
    public string Aisle { get; set; } = string.Empty;
    public string Bin { get; set; } = string.Empty;
    public string Shelf { get; set; } = string.Empty;
    public int Capacity { get; set; }
}

public class UpdateLocationCommandHandler : MediatR.IRequestHandler<UpdateLocationCommand, LocationDto>
{
    private readonly IWarehouseUnitOfWork _uow;

    public UpdateLocationCommandHandler(IWarehouseUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<LocationDto> Handle(UpdateLocationCommand request, CancellationToken cancellationToken)
    {
        var location = await _uow.Locations.GetByIdAsync(request.LocationId, cancellationToken)
            ?? throw new InvalidOperationException($"Location {request.LocationId} not found");

        location.Update(request.Name, request.Zone, request.Aisle, request.Bin, request.Shelf, request.Capacity);
        if (request.WarehouseId.HasValue)
            location.AssignWarehouse(request.WarehouseId);
        await _uow.Locations.UpdateAsync(location, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return CreateLocationCommandHandler.Map(location);
    }
}

public class SetLocationActiveCommand : IRequest<LocationDto>
{
    public Guid LocationId { get; set; }
    public bool IsActive { get; set; }
}

public class SetLocationActiveCommandHandler : MediatR.IRequestHandler<SetLocationActiveCommand, LocationDto>
{
    private readonly IWarehouseUnitOfWork _uow;

    public SetLocationActiveCommandHandler(IWarehouseUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<LocationDto> Handle(SetLocationActiveCommand request, CancellationToken cancellationToken)
    {
        var location = await _uow.Locations.GetByIdAsync(request.LocationId, cancellationToken)
            ?? throw new InvalidOperationException($"Location {request.LocationId} not found");

        if (request.IsActive) location.Activate(); else location.Deactivate();
        await _uow.Locations.UpdateAsync(location, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return CreateLocationCommandHandler.Map(location);
    }
}
