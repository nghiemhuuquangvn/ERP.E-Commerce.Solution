using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.Suppliers.Queries;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.Suppliers.Handlers;

public class GetSupplierItemsQueryHandler : IRequestHandler<GetSupplierItemsQuery, IEnumerable<SupplierItemDto>>
{
    private readonly IRepository<SupplierItem> _repository;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public GetSupplierItemsQueryHandler(
        IRepository<SupplierItem> repository,
        IUserContextService userContextService,
        IMapper mapper)
    {
        _repository = repository;
        _userContextService = userContextService;
        _mapper = mapper;
    }

    public async Task<IEnumerable<SupplierItemDto>> Handle(GetSupplierItemsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var items = (await _repository.GetAllAsync(tenantId, cancellationToken))
            .Where(i => i.SupplierId == request.SupplierId)
            .OrderBy(i => i.SupplierItemCode)
            .ToList();

        return _mapper.Map<IEnumerable<SupplierItemDto>>(items);
    }
}

public class GetSupplierItemByIdQueryHandler : IRequestHandler<GetSupplierItemByIdQuery, SupplierItemDto?>
{
    private readonly IRepository<SupplierItem> _repository;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public GetSupplierItemByIdQueryHandler(
        IRepository<SupplierItem> repository,
        IUserContextService userContextService,
        IMapper mapper)
    {
        _repository = repository;
        _userContextService = userContextService;
        _mapper = mapper;
    }

    public async Task<SupplierItemDto?> Handle(GetSupplierItemByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var item = await _repository.GetByIdAsync(request.SupplierItemId, tenantId, cancellationToken);

        if (item == null || item.SupplierId != request.SupplierId)
            return null;

        return _mapper.Map<SupplierItemDto>(item);
    }
}
