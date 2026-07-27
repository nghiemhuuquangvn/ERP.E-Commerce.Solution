using ERP.PO.Domain.Entities;

namespace ERP.PO.Application.Features.PurchaseRequests;

public class PurchaseRequestDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int CurrentQuantity { get; set; }
    public int ReorderPoint { get; set; }
    public int SuggestedQuantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public static PurchaseRequestDto Map(PurchaseRequest r) => new()
    {
        Id = r.Id,
        ProductId = r.ProductId,
        ProductName = r.ProductName,
        CurrentQuantity = r.CurrentQuantity,
        ReorderPoint = r.ReorderPoint,
        SuggestedQuantity = r.SuggestedQuantity,
        Status = r.Status.ToString(),
        CreatedAt = r.CreatedAt,
        ResolvedAt = r.ResolvedAt
    };
}
