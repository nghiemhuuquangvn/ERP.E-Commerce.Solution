using ERP.PO.Domain.Entities;

namespace ERP.PO.Application.Features.PurchaseRequests;

public class PurchaseRequestDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int CurrentQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int OnOrderQuantity { get; set; }
    public int ReorderPoint { get; set; }
    public int MinStock { get; set; }
    public int MaxStock { get; set; }
    public int SafetyStock { get; set; }
    public int SuggestedQuantity { get; set; }
    public Guid? PreferredSupplierId { get; set; }
    public string? PreferredSupplierName { get; set; }
    public decimal? UnitPrice { get; set; }
    public int? LeadTimeDays { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public Guid? RfqId { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public static PurchaseRequestDto Map(PurchaseRequest r) => new()
    {
        Id = r.Id,
        ProductId = r.ProductId,
        ProductCode = r.ProductCode,
        ProductName = r.ProductName,
        CurrentQuantity = r.CurrentQuantity,
        AvailableQuantity = r.AvailableQuantity,
        OnOrderQuantity = r.OnOrderQuantity,
        ReorderPoint = r.ReorderPoint,
        MinStock = r.MinStock,
        MaxStock = r.MaxStock,
        SafetyStock = r.SafetyStock,
        SuggestedQuantity = r.SuggestedQuantity,
        PreferredSupplierId = r.PreferredSupplierId,
        UnitPrice = r.UnitPrice,
        LeadTimeDays = r.LeadTimeDays,
        PurchaseOrderId = r.PurchaseOrderId,
        RfqId = r.RfqId,
        Note = r.Note,
        Status = r.Status.ToString(),
        CreatedAt = r.CreatedAt,
        ResolvedAt = r.ResolvedAt
    };
}
