using MediatR;

namespace ERP.WH.Application.Commands.Locations;

public class CreateLocationCommand : IRequest<LocationDto>
{
    public Guid? WarehouseId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Zone { get; set; }
    public string Aisle { get; set; } = string.Empty;
    public string Bin { get; set; } = string.Empty;
    public string Shelf { get; set; } = string.Empty;
    public int Capacity { get; set; } = 1000;
}

public class LocationDto
{
    public Guid LocationId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Zone { get; set; }
    public string Aisle { get; set; } = string.Empty;
    public string Bin { get; set; } = string.Empty;
    public string Shelf { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int CurrentOccupancy { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
