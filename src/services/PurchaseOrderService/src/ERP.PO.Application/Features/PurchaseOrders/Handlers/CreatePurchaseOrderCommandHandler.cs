using MediatR;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.PurchaseOrders.Commands;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace ERP.PO.Application.Features.PurchaseOrders.Handlers;

public class CreatePurchaseOrderCommandHandler : IRequestHandler<CreatePurchaseOrderCommand, PurchaseOrderDto>
{
    private readonly IRepository<PurchaseOrder> _repository;
    private readonly IUserContextService _userContextService;
    private readonly IMapper _mapper;
    private readonly ILogger<CreatePurchaseOrderCommandHandler> _logger;

    public CreatePurchaseOrderCommandHandler(
        IRepository<PurchaseOrder> repository,
        IUserContextService userContextService,
        IMapper mapper,
        ILogger<CreatePurchaseOrderCommandHandler> logger)
    {
        _repository = repository;
        _userContextService = userContextService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PurchaseOrderDto> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        // ← Extract user context from API Gateway headers
        var user = _userContextService.GetCurrentUser();

        if (string.IsNullOrEmpty(user.TenantId))
            throw new InvalidOperationException("TenantId is required in user context");

        var tenantId = new Guid(user.TenantId);

        // Create aggregate root with all validations
        var purchaseOrder = PurchaseOrder.Create(
            request.PONumber,
            request.SupplierId,
            tenantId,
            request.ExpectedDeliveryDate,
            request.Notes);

        // ← Set audit trail from user context
        purchaseOrder.CreatedBy = user.UserId;
        purchaseOrder.CreatedAt = DateTime.UtcNow;

        // Save to repository
        _repository.Add(purchaseOrder);
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        // ← Audit log
        _logger.LogInformation(
            "Purchase Order {PONumber} created by user {UserId} in tenant {TenantId}",
            purchaseOrder.PONumber,
            user.UserId,
            user.TenantId);

        return _mapper.Map<PurchaseOrderDto>(purchaseOrder);
    }
}
