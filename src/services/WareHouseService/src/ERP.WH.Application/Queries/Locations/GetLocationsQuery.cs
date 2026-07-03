using MediatR;
using ERP.WH.Application.Commands.Locations;

namespace ERP.WH.Application.Queries.Locations;

public class GetLocationsQuery : IRequest<List<LocationDto>>
{
    public bool? IsActive { get; set; }
}

public class GetLocationsQueryHandler : IRequestHandler<GetLocationsQuery, List<LocationDto>>
{
    private readonly ILocationRepository _locationRepository;

    public GetLocationsQueryHandler(ILocationRepository locationRepository)
    {
        _locationRepository = locationRepository;
    }

    public async Task<List<LocationDto>> Handle(GetLocationsQuery request, CancellationToken cancellationToken)
    {
        var locations = await _locationRepository.GetAllAsync(request.IsActive, cancellationToken);
        return locations.Select(CreateLocationCommandHandler.Map).ToList();
    }
}

public class GetLocationByIdQuery : IRequest<LocationDto?>
{
    public Guid LocationId { get; set; }
}

public class GetLocationByIdQueryHandler : IRequestHandler<GetLocationByIdQuery, LocationDto?>
{
    private readonly ILocationRepository _locationRepository;

    public GetLocationByIdQueryHandler(ILocationRepository locationRepository)
    {
        _locationRepository = locationRepository;
    }

    public async Task<LocationDto?> Handle(GetLocationByIdQuery request, CancellationToken cancellationToken)
    {
        var location = await _locationRepository.GetByIdAsync(request.LocationId, cancellationToken);
        return location == null ? null : CreateLocationCommandHandler.Map(location);
    }
}
