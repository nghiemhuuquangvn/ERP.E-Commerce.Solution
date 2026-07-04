using ERP.PRM.Application.DTOs;
using ERP.PRM.Domain.Enums;
using MediatR;

namespace ERP.PRM.Application.Commands;

public class CreatePromotionCommand : IRequest<PromotionDto>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DiscountType DiscountType { get; set; }
    public PromotionScope Scope { get; set; } = PromotionScope.Order;
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal MinOrderAmount { get; set; }
    public int? MaxUsageTotal { get; set; }
    public int? MaxUsagePerUser { get; set; }
    public int? BuyQuantity { get; set; }
    public int? GetQuantity { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<Guid> ApplicableProductIds { get; set; } = new();
    public List<string> ApplicableCategoryIds { get; set; } = new();
}

public class ActivatePromotionCommand : IRequest<PromotionDto>
{
    public Guid PromotionId { get; set; }
}

public class PausePromotionCommand : IRequest<PromotionDto>
{
    public Guid PromotionId { get; set; }
}

public class CancelPromotionCommand : IRequest<PromotionDto>
{
    public Guid PromotionId { get; set; }
}

public class GenerateBulkCouponsCommand : IRequest<BulkCouponResult>
{
    public Guid PromotionId { get; set; }
    public int Count { get; set; }
    public string Prefix { get; set; } = string.Empty;
}

public class RedeemCouponCommand : IRequest<CouponValidationResult>
{
    public string Code { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public decimal OrderAmount { get; set; }
}

public class EarnLoyaltyPointsCommand : IRequest<LoyaltyAccountDto>
{
    public Guid UserId { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class RedeemLoyaltyPointsCommand : IRequest<LoyaltyAccountDto>
{
    public Guid UserId { get; set; }
    public int Points { get; set; }
}

// Flash sale commands
public class CreateFlashSaleCommand : IRequest<FlashSaleDto>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<FlashSaleItemRequest> Items { get; set; } = new();
}

public class FlashSaleItemRequest
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal OriginalPrice { get; set; }
    public decimal SalePrice { get; set; }
    public int AllocatedQuantity { get; set; }
}

public class StartFlashSaleCommand : IRequest<FlashSaleDto>
{
    public Guid FlashSaleId { get; set; }
}

public class CancelFlashSaleCommand : IRequest<FlashSaleDto>
{
    public Guid FlashSaleId { get; set; }
}

public class ReserveFlashSaleItemCommand : IRequest<bool>
{
    public Guid FlashSaleId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
