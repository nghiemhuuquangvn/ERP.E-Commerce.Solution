namespace ERP.PRM.Application.DTOs;

public class PromotionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DiscountType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal MinOrderAmount { get; set; }
    public int? MaxUsageTotal { get; set; }
    public int? MaxUsagePerUser { get; set; }
    public int CurrentUsage { get; set; }
    public int? BuyQuantity { get; set; }
    public int? GetQuantity { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsCurrentlyActive { get; set; }
    public List<Guid> ApplicableProductIds { get; set; } = new();
    public List<string> ApplicableCategoryIds { get; set; } = new();
}

public class CouponValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal DiscountAmount { get; set; }
    public Guid? PromotionId { get; set; }
    public string? CouponCode { get; set; }
}

public class LoyaltyAccountDto
{
    public Guid UserId { get; set; }
    public int TotalPoints { get; set; }
    public int AvailablePoints { get; set; }
    public string Tier { get; set; } = string.Empty;
    public int PointsToNextTier { get; set; }
    public string? NextTier { get; set; }
}

public class FlashSaleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<FlashSaleItemDto> Items { get; set; } = new();
}

public class FlashSaleItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal OriginalPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal DiscountPercentage { get; set; }
    public int AllocatedQuantity { get; set; }
    public int SoldQuantity { get; set; }
    public int RemainingQuantity { get; set; }
}

public class ApplicablePromotionDto
{
    public Guid PromotionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }
    public string? Description { get; set; }
    public bool IsFreeShipping { get; set; }
}

public class OrderItemInputDto
{
    public Guid ProductId { get; set; }
    public string Category { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class BulkCouponResult
{
    public Guid PromotionId { get; set; }
    public int Generated { get; set; }
    public List<string> Codes { get; set; } = new();
}
