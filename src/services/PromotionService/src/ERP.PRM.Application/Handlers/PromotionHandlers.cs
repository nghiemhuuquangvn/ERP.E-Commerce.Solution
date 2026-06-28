using ERP.PRM.Application.Commands;
using ERP.PRM.Application.DTOs;
using ERP.PRM.Application.Interfaces;
using ERP.PRM.Application.Queries;
using ERP.PRM.Domain.Entities;
using MediatR;

namespace ERP.PRM.Application.Handlers;

public class CreatePromotionCommandHandler : IRequestHandler<CreatePromotionCommand, PromotionDto>
{
    private readonly IPromotionRepository _promotions;
    public CreatePromotionCommandHandler(IPromotionRepository promotions) => _promotions = promotions;

    public async Task<PromotionDto> Handle(CreatePromotionCommand request, CancellationToken cancellationToken)
    {
        var promo = Promotion.Create(request.Name, request.DiscountType, request.DiscountValue,
            request.StartDate, request.EndDate, request.MinOrderAmount, request.MaxUsageTotal, request.Description);

        await _promotions.AddAsync(promo, cancellationToken);
        return MapToDto(promo);
    }

    internal static PromotionDto MapToDto(Promotion p) => new()
    {
        Id = p.Id, Name = p.Name, Description = p.Description,
        DiscountType = p.DiscountType.ToString(), DiscountValue = p.DiscountValue,
        MinOrderAmount = p.MinOrderAmount, MaxUsageTotal = p.MaxUsageTotal,
        CurrentUsage = p.CurrentUsage, StartDate = p.StartDate, EndDate = p.EndDate,
        Status = p.Status.ToString(), IsCurrentlyActive = p.IsActive()
    };
}

public class ActivatePromotionCommandHandler : IRequestHandler<ActivatePromotionCommand, PromotionDto>
{
    private readonly IPromotionRepository _promotions;
    public ActivatePromotionCommandHandler(IPromotionRepository promotions) => _promotions = promotions;

    public async Task<PromotionDto> Handle(ActivatePromotionCommand request, CancellationToken cancellationToken)
    {
        var promo = await _promotions.GetByIdAsync(request.PromotionId, cancellationToken)
            ?? throw new InvalidOperationException($"Promotion {request.PromotionId} not found.");

        promo.Activate();
        await _promotions.UpdateAsync(promo, cancellationToken);
        return CreatePromotionCommandHandler.MapToDto(promo);
    }
}

public class RedeemCouponCommandHandler : IRequestHandler<RedeemCouponCommand, CouponValidationResult>
{
    private readonly ICouponRepository _coupons;
    private readonly IPromotionRepository _promotions;

    public RedeemCouponCommandHandler(ICouponRepository coupons, IPromotionRepository promotions)
    {
        _coupons = coupons;
        _promotions = promotions;
    }

    public async Task<CouponValidationResult> Handle(RedeemCouponCommand request, CancellationToken cancellationToken)
    {
        var coupon = await _coupons.GetByCodeAsync(request.Code.ToUpperInvariant(), cancellationToken);
        if (coupon == null)
            return new() { IsValid = false, ErrorMessage = "Coupon code not found." };

        if (coupon.IsUsed)
            return new() { IsValid = false, ErrorMessage = "Coupon has already been used." };

        var promotion = await _promotions.GetByIdAsync(coupon.PromotionId, cancellationToken);
        if (promotion == null || !promotion.IsActive())
            return new() { IsValid = false, ErrorMessage = "Promotion is not active." };

        var discount = promotion.CalculateDiscount(request.OrderAmount);
        coupon.Redeem(request.UserId);
        promotion.RecordUsage();

        await _coupons.UpdateAsync(coupon, cancellationToken);
        await _promotions.UpdateAsync(promotion, cancellationToken);

        return new() { IsValid = true, DiscountAmount = discount, PromotionId = promotion.Id };
    }
}

public class EarnLoyaltyPointsCommandHandler : IRequestHandler<EarnLoyaltyPointsCommand, LoyaltyAccountDto>
{
    private readonly ILoyaltyAccountRepository _accounts;
    public EarnLoyaltyPointsCommandHandler(ILoyaltyAccountRepository accounts) => _accounts = accounts;

    public async Task<LoyaltyAccountDto> Handle(EarnLoyaltyPointsCommand request, CancellationToken cancellationToken)
    {
        var account = await _accounts.GetByUserIdAsync(request.UserId, cancellationToken);
        if (account == null)
        {
            account = LoyaltyAccount.Create(request.UserId);
            account.EarnPoints(request.Points, request.Reason);
            await _accounts.AddAsync(account, cancellationToken);
        }
        else
        {
            account.EarnPoints(request.Points, request.Reason);
            await _accounts.UpdateAsync(account, cancellationToken);
        }

        return new() { UserId = account.UserId, TotalPoints = account.TotalPoints,
            AvailablePoints = account.AvailablePoints, Tier = account.Tier.ToString() };
    }
}

public class GetActivePromotionsQueryHandler : IRequestHandler<GetActivePromotionsQuery, List<PromotionDto>>
{
    private readonly IPromotionRepository _promotions;
    public GetActivePromotionsQueryHandler(IPromotionRepository promotions) => _promotions = promotions;

    public async Task<List<PromotionDto>> Handle(GetActivePromotionsQuery request, CancellationToken cancellationToken)
    {
        var promotions = await _promotions.GetActiveAsync(cancellationToken);
        return promotions.Select(CreatePromotionCommandHandler.MapToDto).ToList();
    }
}

public class ValidateCouponQueryHandler : IRequestHandler<ValidateCouponQuery, CouponValidationResult>
{
    private readonly ICouponRepository _coupons;
    private readonly IPromotionRepository _promotions;

    public ValidateCouponQueryHandler(ICouponRepository coupons, IPromotionRepository promotions)
    {
        _coupons = coupons;
        _promotions = promotions;
    }

    public async Task<CouponValidationResult> Handle(ValidateCouponQuery request, CancellationToken cancellationToken)
    {
        var coupon = await _coupons.GetByCodeAsync(request.Code.ToUpperInvariant(), cancellationToken);
        if (coupon == null || coupon.IsUsed)
            return new() { IsValid = false, ErrorMessage = coupon == null ? "Coupon not found." : "Already used." };

        var promotion = await _promotions.GetByIdAsync(coupon.PromotionId, cancellationToken);
        if (promotion == null || !promotion.IsActive())
            return new() { IsValid = false, ErrorMessage = "Promotion not active." };

        return new() { IsValid = true, DiscountAmount = promotion.CalculateDiscount(request.OrderAmount), PromotionId = promotion.Id };
    }
}

public class GetLoyaltyBalanceQueryHandler : IRequestHandler<GetLoyaltyBalanceQuery, LoyaltyAccountDto?>
{
    private readonly ILoyaltyAccountRepository _accounts;
    public GetLoyaltyBalanceQueryHandler(ILoyaltyAccountRepository accounts) => _accounts = accounts;

    public async Task<LoyaltyAccountDto?> Handle(GetLoyaltyBalanceQuery request, CancellationToken cancellationToken)
    {
        var account = await _accounts.GetByUserIdAsync(request.UserId, cancellationToken);
        if (account == null) return null;
        return new() { UserId = account.UserId, TotalPoints = account.TotalPoints,
            AvailablePoints = account.AvailablePoints, Tier = account.Tier.ToString() };
    }
}
