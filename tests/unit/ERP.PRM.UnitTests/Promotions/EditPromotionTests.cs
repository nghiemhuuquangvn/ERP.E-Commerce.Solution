using ERP.PRM.Application.Commands;
using ERP.PRM.Application.Handlers;
using ERP.PRM.Application.Interfaces;
using ERP.PRM.Application.Queries;
using ERP.PRM.Domain.Entities;
using ERP.PRM.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.PRM.UnitTests.Promotions;

/// <summary>ROADMAP P2-1a: the back office edits a draft promotion and lists its coupons.</summary>
public class EditPromotionTests
{
    private readonly Mock<IPromotionUnitOfWork> _uow = new();
    private readonly Mock<IPromotionRepository> _promotions = new();

    public EditPromotionTests() => _uow.SetupGet(u => u.Promotions).Returns(_promotions.Object);

    private static Promotion Draft() =>
        Promotion.Create("Spring", DiscountType.Percentage, 10, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(10));

    private static UpdatePromotionCommand Change(Guid id) => new()
    {
        PromotionId = id, Name = "Summer", DiscountType = DiscountType.FixedAmount, DiscountValue = 50_000,
        StartDate = DateTime.UtcNow.AddDays(2), EndDate = DateTime.UtcNow.AddDays(20),
        ApplicableProductIds = new() { Guid.NewGuid() },
    };

    private UpdatePromotionCommandHandler Handler(Promotion? promo)
    {
        _promotions.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(promo);
        return new UpdatePromotionCommandHandler(_uow.Object);
    }

    [Fact]
    public async Task A_draft_promotion_takes_the_new_terms()
    {
        var promo = Draft();
        var dto = await Handler(promo).Handle(Change(promo.Id), default);

        dto.Name.Should().Be("Summer");
        dto.DiscountType.Should().Be("FixedAmount");
        dto.DiscountValue.Should().Be(50_000);
        promo.ApplicableProductIds.Should().HaveCount(1);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("pause")]
    [InlineData("cancel")]
    public async Task A_promotion_that_left_draft_is_refused_as_a_business_rule(string step)
    {
        var promo = Draft();
        if (step == "activate") promo.Activate();
        if (step == "pause") promo.Pause();
        if (step == "cancel") promo.Cancel();

        var act = () => Handler(promo).Handle(Change(promo.Id), default);

        await act.Should().ThrowAsync<BusinessRuleException>();
        promo.Name.Should().Be("Spring");
    }

    [Fact]
    public async Task Bad_dates_and_a_missing_promotion_are_refused_with_the_right_kind_of_error()
    {
        var promo = Draft();
        var bad = Change(promo.Id);
        bad.EndDate = bad.StartDate;
        await ((Func<Task>)(() => Handler(promo).Handle(bad, default))).Should().ThrowAsync<BusinessRuleException>();

        await ((Func<Task>)(() => Handler(null).Handle(Change(Guid.NewGuid()), default))).Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Coupons_of_a_promotion_are_listed_with_their_use()
    {
        var promo = Draft();
        var codes = promo.GenerateBulkCoupons(3, "sp");
        codes[0].Redeem("user-1", 5_000);
        _promotions.Setup(p => p.GetByIdAsync(promo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(promo);

        var list = await new GetPromotionCouponsQueryHandler(_promotions.Object).Handle(new GetPromotionCouponsQuery { PromotionId = promo.Id }, default);

        list.Should().HaveCount(3);
        list.Count(c => c.IsUsed).Should().Be(1);
        list.Single(c => c.IsUsed).UsedByUserId.Should().Be("user-1");
        list.Should().OnlyContain(c => c.Code.StartsWith("SP-"));
    }
}
