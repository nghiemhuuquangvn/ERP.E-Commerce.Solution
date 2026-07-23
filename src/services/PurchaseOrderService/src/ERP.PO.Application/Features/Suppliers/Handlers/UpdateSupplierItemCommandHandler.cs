using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.Suppliers.Commands;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.Suppliers.Handlers;

public class UpdateSupplierItemCommandHandler : IRequestHandler<UpdateSupplierItemCommand, SupplierItemDto>
{
    private readonly IRepository<SupplierItem> _itemRepository;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public UpdateSupplierItemCommandHandler(
        IRepository<SupplierItem> itemRepository,
        IUserContextService userContextService,
        IMapper mapper)
    {
        _itemRepository = itemRepository;
        _userContextService = userContextService;
        _mapper = mapper;
    }

    public async Task<SupplierItemDto> Handle(UpdateSupplierItemCommand request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var item = await _itemRepository.GetByIdAsync(request.SupplierItemId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"SupplierItem {request.SupplierItemId} not found");

        if (item.SupplierId != request.SupplierId)
            throw new KeyNotFoundException($"SupplierItem {request.SupplierItemId} not found for supplier {request.SupplierId}");

        item.Update(request.LastPurchasePrice, request.LeadTimeDays, request.SupplierItemCode);

        _itemRepository.Update(item);
        await _itemRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<SupplierItemDto>(item);
    }
}
