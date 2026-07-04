using ERP.PRM.Application.DTOs;
using MediatR;

namespace ERP.PRM.Application.Queries;

public class GetActivePromotionsQuery : IRequest<List<PromotionDto>> { }

public class GetAllPromotionsQuery : IRequest<List<PromotionDto>> { }

public class GetPromotionByIdQuery : IRequest<PromotionDto?>
{
    public Guid PromotionId { get; set; }
}

public class ValidateCouponQuery : IRequest<CouponValidationResult>
{
    public string Code { get; set; } = string.Empty;
    public decimal OrderAmount { get; set; }
}

public class GetLoyaltyBalanceQuery : IRequest<LoyaltyAccountDto?>
{
    public Guid UserId { get; set; }
}

public class GetApplicablePromotionsQuery : IRequest<List<ApplicablePromotionDto>>
{
    public Guid CustomerId { get; set; }
    public decimal OrderAmount { get; set; }
    public List<OrderItemInputDto> Items { get; set; } = new();
}

// Flash sale queries
public class GetFlashSalesQuery : IRequest<List<FlashSaleDto>>
{
    public bool ActiveOnly { get; set; } = false;
}

public class GetFlashSaleByIdQuery : IRequest<FlashSaleDto?>
{
    public Guid FlashSaleId { get; set; }
}
