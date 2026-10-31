using ERP.SHP.Application.DTOs;
using ERP.SHP.Application.Handlers;
using ERP.SHP.Application.Interfaces;
using MediatR;

namespace ERP.SHP.Application.Queries;

public class GetReturnShipmentByIdQuery : IRequest<ReturnShipmentDto?>
{
    public Guid ReturnShipmentId { get; set; }
}

public class GetReturnShipmentByIdQueryHandler : IRequestHandler<GetReturnShipmentByIdQuery, ReturnShipmentDto?>
{
    private readonly IReturnShipmentRepository _returns;
    public GetReturnShipmentByIdQueryHandler(IReturnShipmentRepository returns) => _returns = returns;

    public async Task<ReturnShipmentDto?> Handle(GetReturnShipmentByIdQuery request, CancellationToken cancellationToken)
    {
        var returnShipment = await _returns.GetByIdAsync(request.ReturnShipmentId, cancellationToken);
        return returnShipment == null ? null : InitiateReturnCommandHandler.MapToDto(returnShipment);
    }
}

public class ListReturnsByShipmentQuery : IRequest<List<ReturnShipmentDto>>
{
    public Guid OriginalShipmentId { get; set; }
}

public class ListReturnsByShipmentQueryHandler : IRequestHandler<ListReturnsByShipmentQuery, List<ReturnShipmentDto>>
{
    private readonly IReturnShipmentRepository _returns;
    public ListReturnsByShipmentQueryHandler(IReturnShipmentRepository returns) => _returns = returns;

    public async Task<List<ReturnShipmentDto>> Handle(ListReturnsByShipmentQuery request, CancellationToken cancellationToken)
    {
        var returns = await _returns.GetByOriginalShipmentIdAsync(request.OriginalShipmentId, cancellationToken);
        return returns.Select(InitiateReturnCommandHandler.MapToDto).ToList();
    }
}

/// <summary>Return parcels of the shop for the admin Returns screen, newest first.</summary>
public class ListReturnsQuery : IRequest<ReturnShipmentListResult>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    /// <summary>Requested / PickedUp / Received / Cancelled.</summary>
    public string? Status { get; set; }
    public string? Search { get; set; }
    public bool NeedsAttention { get; set; }
}

public class ListReturnsQueryHandler : IRequestHandler<ListReturnsQuery, ReturnShipmentListResult>
{
    private readonly IReturnShipmentRepository _returns;
    public ListReturnsQueryHandler(IReturnShipmentRepository returns) => _returns = returns;

    public async Task<ReturnShipmentListResult> Handle(ListReturnsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);
        ERP.SHP.Domain.Entities.ReturnShipmentStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<ERP.SHP.Domain.Entities.ReturnShipmentStatus>(request.Status, true, out var parsed) || !Enum.IsDefined(parsed))
                throw new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("Shipping.Returns", $"Unknown return status '{request.Status}'.");
            status = parsed;
        }
        var (items, total) = await _returns.ListAsync((page - 1) * size, size, status, request.Search, request.NeedsAttention, cancellationToken);
        return new ReturnShipmentListResult { Items = items.Select(InitiateReturnCommandHandler.MapToDto).ToList(), TotalCount = total, PageNumber = page, PageSize = size };
    }
}
