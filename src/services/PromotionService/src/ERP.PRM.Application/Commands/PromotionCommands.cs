using ERP.PRM.Application.DTOs;
using ERP.PRM.Domain.Enums;
using MediatR;

namespace ERP.PRM.Application.Commands;

public class CreatePromotionCommand : IRequest<PromotionDto>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal MinOrderAmount { get; set; }
    public int? MaxUsageTotal { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class ActivatePromotionCommand : IRequest<PromotionDto>
{
    public Guid PromotionId { get; set; }
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
