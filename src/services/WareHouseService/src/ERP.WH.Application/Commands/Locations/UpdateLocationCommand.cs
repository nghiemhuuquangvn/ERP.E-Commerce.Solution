using MediatR;

namespace ERP.WH.Application.Commands.Locations;

public class UpdateLocationCommand : IRequest<LocationDto>
{
    public Guid LocationId { get; set; }
    public string? Name { get; set; }
    public string? Zone { get; set; }
    public string Aisle { get; set; } = string.Empty;
    public string Bin { get; set; } = string.Empty;
    public string Shelf { get; set; } = string.Empty;
    public int Capacity { get; set; }
}

public class UpdateLocationCommandHandler : MediatR.IRequestHandler<UpdateLocationCommand, LocationDto>
{
    private readonly ILocationRepository _locationRepository;

    public UpdateLocationCommandHandler(ILocationRepository locationRepository)
    {
        _locationRepository = locationRepository;
    }

    public async Task<LocationDto> Handle(UpdateLocationCommand request, CancellationToken cancellationToken)
    {
        var location = await _locationRepository.GetByIdAsync(request.LocationId, cancellationToken)
            ?? throw new InvalidOperationException($"Location {request.LocationId} not found");

        location.Update(request.Name, request.Zone, request.Aisle, request.Bin, request.Shelf, request.Capacity);
        await _locationRepository.UpdateAsync(location, cancellationToken);

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
    private readonly ILocationRepository _locationRepository;

    public SetLocationActiveCommandHandler(ILocationRepository locationRepository)
    {
        _locationRepository = locationRepository;
    }

    public async Task<LocationDto> Handle(SetLocationActiveCommand request, CancellationToken cancellationToken)
    {
        var location = await _locationRepository.GetByIdAsync(request.LocationId, cancellationToken)
            ?? throw new InvalidOperationException($"Location {request.LocationId} not found");

        if (request.IsActive) location.Activate(); else location.Deactivate();
        await _locationRepository.UpdateAsync(location, cancellationToken);

        return CreateLocationCommandHandler.Map(location);
    }
}
