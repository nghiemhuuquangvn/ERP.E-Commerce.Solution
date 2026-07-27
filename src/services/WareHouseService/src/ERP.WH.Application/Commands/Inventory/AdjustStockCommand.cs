using MediatR;
using ERP.WH.Domain.Aggregates.Inventory;

namespace ERP.WH.Application.Commands.Inventory;

/// <summary>Requests a stock change — does NOT touch Inventory. Must be approved via ApproveStockAdjustmentCommand before it takes effect (business rule: adjustments always require Warehouse Manager approval).</summary>
public class RequestStockAdjustmentCommand : IRequest<StockAdjustmentDto>
{
    public Guid ProductId { get; set; }
    public Guid? LocationId { get; set; }
    public int QuantityDelta { get; set; }
    public AdjustmentReason Reason { get; set; }
    public string? Notes { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
}

public class ApproveStockAdjustmentCommand : IRequest<AdjustStockResponse>
{
    public Guid AdjustmentId { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;
}

public class RejectStockAdjustmentCommand : IRequest<StockAdjustmentDto>
{
    public Guid AdjustmentId { get; set; }
    public string RejectedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class StockAdjustmentDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? LocationId { get; set; }
    public int QuantityDelta { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public class AdjustStockResponse
{
    public Guid ProductId { get; set; }
    public int PreviousQuantity { get; set; }
    public int CurrentQuantity { get; set; }
    public Guid MovementId { get; set; }
    public DateTime AdjustedAt { get; set; }
}

public interface IStockAdjustmentRepository
{
    Task<StockAdjustment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<List<StockAdjustment>> ListAsync(StockAdjustmentStatus? status, CancellationToken cancellationToken);
    Task AddAsync(StockAdjustment adjustment, CancellationToken cancellationToken);
    Task UpdateAsync(StockAdjustment adjustment, CancellationToken cancellationToken);
}
