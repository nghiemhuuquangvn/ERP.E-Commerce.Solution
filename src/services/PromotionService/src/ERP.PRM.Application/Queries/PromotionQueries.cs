using ERP.PRM.Application.DTOs;
using MediatR;

namespace ERP.PRM.Application.Queries;

public class GetActivePromotionsQuery : IRequest<List<PromotionDto>> { }

public class ValidateCouponQuery : IRequest<CouponValidationResult>
{
    public string Code { get; set; } = string.Empty;
    public decimal OrderAmount { get; set; }
}

public class GetLoyaltyBalanceQuery : IRequest<LoyaltyAccountDto?>
{
    public Guid UserId { get; set; }
}
