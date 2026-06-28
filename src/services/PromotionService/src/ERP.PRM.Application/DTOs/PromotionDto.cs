namespace ERP.PRM.Application.DTOs;

public class PromotionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public decimal MinOrderAmount { get; set; }
    public int? MaxUsageTotal { get; set; }
    public int CurrentUsage { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsCurrentlyActive { get; set; }
}

public class CouponValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal DiscountAmount { get; set; }
    public Guid? PromotionId { get; set; }
}

public class LoyaltyAccountDto
{
    public Guid UserId { get; set; }
    public int TotalPoints { get; set; }
    public int AvailablePoints { get; set; }
    public string Tier { get; set; } = string.Empty;
}
