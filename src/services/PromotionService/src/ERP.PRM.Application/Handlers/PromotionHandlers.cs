using ERP.PRM.Application.Commands;
using ERP.PRM.Application.DTOs;
using ERP.PRM.Application.Interfaces;
using ERP.PRM.Application.Queries;
using ERP.PRM.Domain.Entities;
using ERP.PRM.Domain.Enums;
using MediatR;

namespace ERP.PRM.Application.Handlers;

// ── Promotion ──────────────────────────────────────────────────────────────────

public class CreatePromotionCommandHandler : IRequestHandler<CreatePromotionCommand, PromotionDto>
{
    private readonly IPromotionRepository _promotions;
    public CreatePromotionCommandHandler(IPromotionRepository promotions) => _promotions = promotions;

    public async Task<PromotionDto> Handle(CreatePromotionCommand request, CancellationToken cancellationToken)
    {
        var promo = Promotion.Create(
            request.Name, request.DiscountType, request.DiscountValue,
            request.StartDate, request.EndDate,
            request.MinOrderAmount, request.MaxUsageTotal, request.MaxUsagePerUser,
            request.Description, request.Scope, request.MaxDiscountAmount,
            request.BuyQuantity, request.GetQuantity);

        foreach (var pid in request.ApplicableProductIds) promo.AddApplicableProduct(pid);
        foreach (var cat in request.ApplicableCategoryIds) promo.AddApplicableCategory(cat);

        await _promotions.AddAsync(promo, cancellationToken);
        return MapToDto(promo);
    }

    internal static PromotionDto MapToDto(Promotion p) => new()
    {
        Id = p.Id, Name = p.Name, Description = p.Description,
        DiscountType = p.DiscountType.ToString(), Scope = p.Scope.ToString(),
        DiscountValue = p.DiscountValue, MaxDiscountAmount = p.MaxDiscountAmount,
        MinOrderAmount = p.MinOrderAmount, MaxUsageTotal = p.MaxUsageTotal,
        MaxUsagePerUser = p.MaxUsagePerUser, CurrentUsage = p.CurrentUsage,
        BuyQuantity = p.BuyQuantity, GetQuantity = p.GetQuantity,
        StartDate = p.StartDate, EndDate = p.EndDate,
        Status = p.Status.ToString(), IsCurrentlyActive = p.IsActive(),
        ApplicableProductIds = p.ApplicableProductIds.ToList(),
        ApplicableCategoryIds = p.ApplicableCategoryIds.ToList()
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

public class PausePromotionCommandHandler : IRequestHandler<PausePromotionCommand, PromotionDto>
{
    private readonly IPromotionRepository _promotions;
    public PausePromotionCommandHandler(IPromotionRepository promotions) => _promotions = promotions;

    public async Task<PromotionDto> Handle(PausePromotionCommand request, CancellationToken cancellationToken)
    {
        var promo = await _promotions.GetByIdAsync(request.PromotionId, cancellationToken)
            ?? throw new InvalidOperationException($"Promotion {request.PromotionId} not found.");
        promo.Pause();
        await _promotions.UpdateAsync(promo, cancellationToken);
        return CreatePromotionCommandHandler.MapToDto(promo);
    }
}

public class CancelPromotionCommandHandler : IRequestHandler<CancelPromotionCommand, PromotionDto>
{
    private readonly IPromotionRepository _promotions;
    public CancelPromotionCommandHandler(IPromotionRepository promotions) => _promotions = promotions;

    public async Task<PromotionDto> Handle(CancelPromotionCommand request, CancellationToken cancellationToken)
    {
        var promo = await _promotions.GetByIdAsync(request.PromotionId, cancellationToken)
            ?? throw new InvalidOperationException($"Promotion {request.PromotionId} not found.");
        promo.Cancel();
        await _promotions.UpdateAsync(promo, cancellationToken);
        return CreatePromotionCommandHandler.MapToDto(promo);
    }
}

public class GenerateBulkCouponsCommandHandler : IRequestHandler<GenerateBulkCouponsCommand, BulkCouponResult>
{
    private readonly IPromotionRepository _promotions;
    public GenerateBulkCouponsCommandHandler(IPromotionRepository promotions) => _promotions = promotions;

    public async Task<BulkCouponResult> Handle(GenerateBulkCouponsCommand request, CancellationToken cancellationToken)
    {
        if (request.Count <= 0 || request.Count > 10000)
            throw new InvalidOperationException("Count must be between 1 and 10,000.");

        var promo = await _promotions.GetByIdAsync(request.PromotionId, cancellationToken)
            ?? throw new InvalidOperationException($"Promotion {request.PromotionId} not found.");

        var coupons = promo.GenerateBulkCoupons(request.Count, request.Prefix);
        await _promotions.UpdateAsync(promo, cancellationToken);

        return new BulkCouponResult
        {
            PromotionId = promo.Id,
            Generated = coupons.Count,
            Codes = coupons.Select(c => c.Code).ToList()
        };
    }
}

// ── Coupon ─────────────────────────────────────────────────────────────────────

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
        coupon.Redeem(request.UserId, discount);
        promotion.RecordUsage();

        await _coupons.UpdateAsync(coupon, cancellationToken);
        await _promotions.UpdateAsync(promotion, cancellationToken);

        return new() { IsValid = true, DiscountAmount = discount, PromotionId = promotion.Id, CouponCode = coupon.Code };
    }
}

// ── Loyalty ────────────────────────────────────────────────────────────────────

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

        return MapLoyaltyDto(account);
    }

    internal static LoyaltyAccountDto MapLoyaltyDto(LoyaltyAccount a)
    {
        var (pointsToNext, nextTier) = GetTierProgress(a.TotalPoints, a.Tier);
        return new()
        {
            UserId = a.UserId, TotalPoints = a.TotalPoints,
            AvailablePoints = a.AvailablePoints, Tier = a.Tier.ToString(),
            PointsToNextTier = pointsToNext, NextTier = nextTier
        };
    }

    private static (int pointsToNext, string? nextTier) GetTierProgress(int totalPoints, LoyaltyTier tier) =>
        tier switch
        {
            LoyaltyTier.Bronze => (LoyaltyAccount.SilverThreshold - totalPoints, "Silver"),
            LoyaltyTier.Silver => (LoyaltyAccount.GoldThreshold - totalPoints, "Gold"),
            LoyaltyTier.Gold => (LoyaltyAccount.PlatinumThreshold - totalPoints, "Platinum"),
            _ => (0, null)
        };
}

public class RedeemLoyaltyPointsCommandHandler : IRequestHandler<RedeemLoyaltyPointsCommand, LoyaltyAccountDto>
{
    private readonly ILoyaltyAccountRepository _accounts;
    public RedeemLoyaltyPointsCommandHandler(ILoyaltyAccountRepository accounts) => _accounts = accounts;

    public async Task<LoyaltyAccountDto> Handle(RedeemLoyaltyPointsCommand request, CancellationToken cancellationToken)
    {
        var account = await _accounts.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"Loyalty account not found for user {request.UserId}.");
        account.RedeemPoints(request.Points);
        await _accounts.UpdateAsync(account, cancellationToken);
        return EarnLoyaltyPointsCommandHandler.MapLoyaltyDto(account);
    }
}

// ── Flash Sale ─────────────────────────────────────────────────────────────────

public class CreateFlashSaleCommandHandler : IRequestHandler<CreateFlashSaleCommand, FlashSaleDto>
{
    private readonly IFlashSaleRepository _flashSales;
    public CreateFlashSaleCommandHandler(IFlashSaleRepository flashSales) => _flashSales = flashSales;

    public async Task<FlashSaleDto> Handle(CreateFlashSaleCommand request, CancellationToken cancellationToken)
    {
        var flashSale = FlashSale.Create(request.Name, request.StartTime, request.EndTime, request.Description);

        foreach (var item in request.Items)
            flashSale.AddItem(item.ProductId, item.ProductName, item.OriginalPrice, item.SalePrice, item.AllocatedQuantity);

        await _flashSales.AddAsync(flashSale, cancellationToken);
        return MapToDto(flashSale);
    }

    internal static FlashSaleDto MapToDto(FlashSale fs) => new()
    {
        Id = fs.Id, Name = fs.Name, Description = fs.Description,
        StartTime = fs.StartTime, EndTime = fs.EndTime, Status = fs.Status.ToString(),
        Items = fs.Items.Select(i => new FlashSaleItemDto
        {
            Id = i.Id, ProductId = i.ProductId, ProductName = i.ProductName,
            OriginalPrice = i.OriginalPrice, SalePrice = i.SalePrice,
            DiscountPercentage = i.DiscountPercentage,
            AllocatedQuantity = i.AllocatedQuantity,
            SoldQuantity = i.SoldQuantity,
            RemainingQuantity = i.RemainingQuantity
        }).ToList()
    };
}

public class StartFlashSaleCommandHandler : IRequestHandler<StartFlashSaleCommand, FlashSaleDto>
{
    private readonly IFlashSaleRepository _flashSales;
    public StartFlashSaleCommandHandler(IFlashSaleRepository flashSales) => _flashSales = flashSales;

    public async Task<FlashSaleDto> Handle(StartFlashSaleCommand request, CancellationToken cancellationToken)
    {
        var flashSale = await _flashSales.GetByIdAsync(request.FlashSaleId, cancellationToken)
            ?? throw new InvalidOperationException($"Flash sale {request.FlashSaleId} not found.");
        flashSale.Start();
        await _flashSales.UpdateAsync(flashSale, cancellationToken);
        return CreateFlashSaleCommandHandler.MapToDto(flashSale);
    }
}

public class CancelFlashSaleCommandHandler : IRequestHandler<CancelFlashSaleCommand, FlashSaleDto>
{
    private readonly IFlashSaleRepository _flashSales;
    public CancelFlashSaleCommandHandler(IFlashSaleRepository flashSales) => _flashSales = flashSales;

    public async Task<FlashSaleDto> Handle(CancelFlashSaleCommand request, CancellationToken cancellationToken)
    {
        var flashSale = await _flashSales.GetByIdAsync(request.FlashSaleId, cancellationToken)
            ?? throw new InvalidOperationException($"Flash sale {request.FlashSaleId} not found.");
        flashSale.Cancel();
        await _flashSales.UpdateAsync(flashSale, cancellationToken);
        return CreateFlashSaleCommandHandler.MapToDto(flashSale);
    }
}

public class ReserveFlashSaleItemCommandHandler : IRequestHandler<ReserveFlashSaleItemCommand, bool>
{
    private readonly IFlashSaleRepository _flashSales;
    public ReserveFlashSaleItemCommandHandler(IFlashSaleRepository flashSales) => _flashSales = flashSales;

    public async Task<bool> Handle(ReserveFlashSaleItemCommand request, CancellationToken cancellationToken)
    {
        var flashSale = await _flashSales.GetByIdAsync(request.FlashSaleId, cancellationToken);
        if (flashSale == null) return false;

        var reserved = flashSale.TryReserve(request.ProductId, request.Quantity);
        if (reserved) await _flashSales.UpdateAsync(flashSale, cancellationToken);
        return reserved;
    }
}

// ── Queries ────────────────────────────────────────────────────────────────────

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

public class GetAllPromotionsQueryHandler : IRequestHandler<GetAllPromotionsQuery, List<PromotionDto>>
{
    private readonly IPromotionRepository _promotions;
    public GetAllPromotionsQueryHandler(IPromotionRepository promotions) => _promotions = promotions;

    public async Task<List<PromotionDto>> Handle(GetAllPromotionsQuery request, CancellationToken cancellationToken)
    {
        var promotions = await _promotions.GetAllAsync(cancellationToken);
        return promotions.Select(CreatePromotionCommandHandler.MapToDto).ToList();
    }
}

public class GetPromotionByIdQueryHandler : IRequestHandler<GetPromotionByIdQuery, PromotionDto?>
{
    private readonly IPromotionRepository _promotions;
    public GetPromotionByIdQueryHandler(IPromotionRepository promotions) => _promotions = promotions;

    public async Task<PromotionDto?> Handle(GetPromotionByIdQuery request, CancellationToken cancellationToken)
    {
        var promo = await _promotions.GetByIdAsync(request.PromotionId, cancellationToken);
        return promo == null ? null : CreatePromotionCommandHandler.MapToDto(promo);
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

        return new()
        {
            IsValid = true,
            DiscountAmount = promotion.CalculateDiscount(request.OrderAmount),
            PromotionId = promotion.Id,
            CouponCode = coupon.Code
        };
    }
}

public class GetLoyaltyBalanceQueryHandler : IRequestHandler<GetLoyaltyBalanceQuery, LoyaltyAccountDto?>
{
    private readonly ILoyaltyAccountRepository _accounts;
    public GetLoyaltyBalanceQueryHandler(ILoyaltyAccountRepository accounts) => _accounts = accounts;

    public async Task<LoyaltyAccountDto?> Handle(GetLoyaltyBalanceQuery request, CancellationToken cancellationToken)
    {
        var account = await _accounts.GetByUserIdAsync(request.UserId, cancellationToken);
        return account == null ? null : EarnLoyaltyPointsCommandHandler.MapLoyaltyDto(account);
    }
}

public class GetApplicablePromotionsQueryHandler : IRequestHandler<GetApplicablePromotionsQuery, List<ApplicablePromotionDto>>
{
    private readonly IPromotionRepository _promotions;
    public GetApplicablePromotionsQueryHandler(IPromotionRepository promotions) => _promotions = promotions;

    public async Task<List<ApplicablePromotionDto>> Handle(GetApplicablePromotionsQuery request, CancellationToken cancellationToken)
    {
        var activePromotions = await _promotions.GetActiveAsync(cancellationToken);

        var itemTuples = request.Items
            .Select(i => (i.ProductId, i.Category, i.Quantity, i.UnitPrice))
            .ToList();

        var result = new List<ApplicablePromotionDto>();

        foreach (var promo in activePromotions)
        {
            if (request.OrderAmount < promo.MinOrderAmount) continue;
            if (!promo.IsApplicableToItems(itemTuples)) continue;

            var discount = promo.CalculateDiscount(request.OrderAmount, itemTuples);
            var isFreeShipping = promo.DiscountType == DiscountType.FreeShipping;

            if (discount <= 0 && !isFreeShipping) continue;

            result.Add(new ApplicablePromotionDto
            {
                PromotionId = promo.Id,
                Name = promo.Name,
                DiscountType = promo.DiscountType.ToString(),
                DiscountAmount = discount,
                Description = promo.Description,
                IsFreeShipping = isFreeShipping
            });
        }

        return result.OrderByDescending(r => r.DiscountAmount).ToList();
    }
}

public class GetFlashSalesQueryHandler : IRequestHandler<GetFlashSalesQuery, List<FlashSaleDto>>
{
    private readonly IFlashSaleRepository _flashSales;
    public GetFlashSalesQueryHandler(IFlashSaleRepository flashSales) => _flashSales = flashSales;

    public async Task<List<FlashSaleDto>> Handle(GetFlashSalesQuery request, CancellationToken cancellationToken)
    {
        var list = request.ActiveOnly
            ? await _flashSales.GetActiveAsync(cancellationToken)
            : await _flashSales.GetAllAsync(cancellationToken);

        return list.Select(CreateFlashSaleCommandHandler.MapToDto).ToList();
    }
}

public class GetFlashSaleByIdQueryHandler : IRequestHandler<GetFlashSaleByIdQuery, FlashSaleDto?>
{
    private readonly IFlashSaleRepository _flashSales;
    public GetFlashSaleByIdQueryHandler(IFlashSaleRepository flashSales) => _flashSales = flashSales;

    public async Task<FlashSaleDto?> Handle(GetFlashSaleByIdQuery request, CancellationToken cancellationToken)
    {
        var fs = await _flashSales.GetByIdAsync(request.FlashSaleId, cancellationToken);
        return fs == null ? null : CreateFlashSaleCommandHandler.MapToDto(fs);
    }
}
