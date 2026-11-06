using ERP.WH.Application.Interfaces;
using MediatR;

namespace ERP.WH.Application.Queries.Warehouses;

/// <summary>What ManufacturingService needs to know about the warehouses it is configured with (service-to-service): identity and whether they are in use.</summary>
public class LookupWarehousesQuery : IRequest<List<WarehouseLookupDto>>
{
    public List<Guid> Ids { get; set; } = new();
}

public class WarehouseLookupDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class LookupWarehousesQueryHandler : IRequestHandler<LookupWarehousesQuery, List<WarehouseLookupDto>>
{
    private readonly IWarehouseUnitOfWork _uow;

    public LookupWarehousesQueryHandler(IWarehouseUnitOfWork uow) => _uow = uow;

    public async Task<List<WarehouseLookupDto>> Handle(LookupWarehousesQuery request, CancellationToken cancellationToken)
    {
        var ids = request.Ids.Distinct().ToHashSet();
        if (ids.Count == 0) return new();

        // The tenant filter of the context limits this to the caller's tenant; unknown ids are simply missing.
        var all = await _uow.Warehouses.GetAllAsync(cancellationToken);
        return all.Where(w => ids.Contains(w.Id))
            .Select(w => new WarehouseLookupDto { Id = w.Id, Code = w.Code, Name = w.Name, IsActive = w.IsActive })
            .ToList();
    }
}
