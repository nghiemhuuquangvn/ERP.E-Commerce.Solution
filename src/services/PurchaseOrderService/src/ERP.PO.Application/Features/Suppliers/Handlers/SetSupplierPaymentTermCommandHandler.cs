using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.Suppliers.Commands;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;

namespace ERP.PO.Application.Features.Suppliers.Handlers;

public class SetSupplierPaymentTermCommandHandler : IRequestHandler<SetSupplierPaymentTermCommand, SupplierDto>
{
    private readonly IRepository<Supplier> _repository;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;

    public SetSupplierPaymentTermCommandHandler(
        IRepository<Supplier> repository,
        IUserContextService userContextService,
        IMapper mapper)
    {
        _repository = repository;
        _userContextService = userContextService;
        _mapper = mapper;
    }

    public async Task<SupplierDto> Handle(SetSupplierPaymentTermCommand request, CancellationToken cancellationToken)
    {
        var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);

        var supplier = await _repository.GetByIdAsync(request.SupplierId, tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Supplier {request.SupplierId} not found");

        supplier.SetPaymentTerm(request.PaymentTermId, request.PaymentTermDays);

        _repository.Update(supplier);
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<SupplierDto>(supplier);
    }
}
