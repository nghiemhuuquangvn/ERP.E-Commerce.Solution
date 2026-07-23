using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.Suppliers.Commands;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.Suppliers.Handlers;

public class CreateSupplierItemCommandHandler : IRequestHandler<CreateSupplierItemCommand, SupplierItemDto>
{
    private readonly IRepository<Supplier> _supplierRepository;
    private readonly IRepository<SupplierItem> _itemRepository;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public CreateSupplierItemCommandHandler(
        IRepository<Supplier> supplierRepository,
        IRepository<SupplierItem> itemRepository,
        IUserContextService userContextService,
        IMapper mapper)
    {
        _supplierRepository = supplierRepository;
        _itemRepository = itemRepository;
        _userContextService = userContextService;
        _mapper = mapper;
    }

    public async Task<SupplierItemDto> Handle(CreateSupplierItemCommand request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var supplier = await _supplierRepository.GetByIdAsync(request.SupplierId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Supplier {request.SupplierId} not found");

        var item = SupplierItem.Create(
            supplier.Id,
            tenantId,
            request.ProductId,
            request.LastPurchasePrice,
            request.LeadTimeDays,
            request.VariantId,
            request.SupplierItemCode);

        _itemRepository.Add(item);
        await _itemRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<SupplierItemDto>(item);
    }
}
