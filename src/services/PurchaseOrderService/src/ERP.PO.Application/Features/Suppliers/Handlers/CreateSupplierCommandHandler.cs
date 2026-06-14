using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.Suppliers.Commands;
using ERP.PO.Domain.Entities;
using ERP.PO.Domain.ValueObjects;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace ERP.PO.Application.Features.Suppliers.Handlers;

public class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, SupplierDto>
{
    private readonly IRepository<Supplier> _repository;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;
    private readonly ILogger<CreateSupplierCommandHandler> _logger;

    public CreateSupplierCommandHandler(
        IRepository<Supplier> repository,
        IUserContextService userContextService,
        IMapper mapper,
        ILogger<CreateSupplierCommandHandler> logger)
    {
        _repository = repository;
        _userContextService = userContextService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<SupplierDto> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        var user = _userContextService.GetCurrentUser();

        if (string.IsNullOrEmpty(user.TenantId))
            throw new InvalidOperationException("TenantId is required in user context");

        var tenantId = new Guid(user.TenantId);

        var address = request.Address != null
            ? new Address(request.Address.Street, request.Address.City, request.Address.State,
                         request.Address.PostalCode, request.Address.Country)
            : null;

        var supplier = Supplier.Create(
            request.Code,
            request.Name,
            tenantId,
            request.ContactPerson,
            request.Email,
            request.Phone,
            address);

        // Set audit trail from user context
        supplier.CreatedBy = user.UserId;
        supplier.CreatedAt = DateTime.UtcNow;

        // Save to repository
        _repository.Add(supplier);
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Supplier {Code} created by user {UserId} in tenant {TenantId}",
            supplier.Code,
            user.UserId,
            user.TenantId);

        return _mapper.Map<SupplierDto>(supplier);
    }
}
