using MediatR;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;

namespace ERP.WH.Application.Commands.Locations;

public class CreateLocationCommandHandler : IRequestHandler<CreateLocationCommand, LocationDto>
{
    private readonly IWarehouseUnitOfWork _uow;

    public CreateLocationCommandHandler(IWarehouseUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<LocationDto> Handle(CreateLocationCommand request, CancellationToken cancellationToken)
    {
        var existing = await _uow.Locations.GetByCodeAsync(request.LocationCode, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException($"Location code '{request.LocationCode}' already exists");

        var location = new WarehouseLocation(
            request.LocationCode,
            request.Aisle,
            request.Bin,
            request.Shelf,
            request.Capacity,
            request.Name,
            request.Zone);

        await _uow.Locations.AddAsync(location, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Map(location);
    }

    internal static LocationDto Map(WarehouseLocation location) => new()
    {
        LocationId = location.LocationId,
        LocationCode = location.LocationCode,
        Name = location.Name,
        Zone = location.Zone,
        Aisle = location.Aisle,
        Bin = location.Bin,
        Shelf = location.Shelf,
        Capacity = location.Capacity,
        CurrentOccupancy = location.CurrentOccupancy,
        IsActive = location.IsActive,
        CreatedAt = location.CreatedAt
    };
}

public interface ILocationRepository
{
    Task<WarehouseLocation?> GetByIdAsync(Guid locationId, CancellationToken cancellationToken);
    Task<WarehouseLocation?> GetByCodeAsync(string locationCode, CancellationToken cancellationToken);
    Task<List<WarehouseLocation>> GetAllAsync(bool? isActive, CancellationToken cancellationToken);
    Task AddAsync(WarehouseLocation location, CancellationToken cancellationToken);
    Task UpdateAsync(WarehouseLocation location, CancellationToken cancellationToken);
}
