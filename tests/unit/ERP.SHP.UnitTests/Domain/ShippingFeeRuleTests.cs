using ERP.SHP.Application.DTOs;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Services;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Entities.Settings;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.SHP.UnitTests.Domain;

/// <summary>ROADMAP P1-11c: the delivery fee comes from the shop's price list (province x weight band); the flat fee covers whatever no row matches.</summary>
public class ShippingFeeRuleTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static ShippingFeeRule Rule(string name, int priority, decimal fee, string? provinces = null, int? min = null, int? max = null, bool active = true)
    {
        var rule = ShippingFeeRule.Create(Tenant, name, priority, fee);
        rule.Update(name, priority, active, provinces, min, max, fee);
        return rule;
    }

    private static TenantShippingSettings Settings(decimal flat, decimal threshold)
    {
        var s = TenantShippingSettings.CreateDefault(Tenant);
        s.Update(flat, threshold);
        return s;
    }

    // ── matching ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Ho Chi Minh", true)]
    [InlineData("TP. Hồ Chí Minh", true)]       // diacritics and the "TP." prefix do not matter
    [InlineData("thành phố hồ chí minh", true)]
    [InlineData("Ha Noi", false)]
    public void Provinces_are_compared_without_case_or_diacritics(string province, bool matches) =>
        Rule("HCM", 1, 20000, "Hồ Chí Minh, Bình Dương").Matches(province, 500).Should().Be(matches);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1000, true)]      // bounds are inclusive
    [InlineData(2500, true)]
    [InlineData(3000, true)]
    [InlineData(3001, false)]
    public void A_weight_band_includes_both_ends(int grams, bool matches) =>
        Rule("band", 1, 10, null, 1000, 3000).Matches("Ha Noi", grams).Should().Be(matches);

    [Fact]
    public void An_inactive_row_never_matches() => Rule("off", 1, 10, active: false).Matches("Ha Noi", 100).Should().BeFalse();

    [Fact]
    public void A_row_with_no_conditions_matches_everything() => Rule("all", 1, 10).Matches("anywhere", 99999).Should().BeTrue();

    [Fact]
    public void A_row_refuses_nonsense()
    {
        var rule = Rule("r", 1, 10);
        ((Action)(() => rule.Update("r", 1, true, null, null, null, -1))).Should().Throw<ArgumentException>();
        ((Action)(() => rule.Update("r", 1, true, null, 3000, 1000, 5))).Should().Throw<ArgumentException>();
        ((Action)(() => rule.Update(" ", 1, true, null, null, null, 5))).Should().Throw<ArgumentException>();
    }

    // ── the fee ───────────────────────────────────────────────────────────

    private static readonly List<ShippingFeeRule> PriceList = new()
    {
        Rule("HCM light", 10, 20000, "Ho Chi Minh", 0, 1000),
        Rule("HCM heavy", 20, 30000, "Ho Chi Minh", 1001, null),
        Rule("Everywhere else", 90, 45000),
    };

    [Theory]
    [InlineData("Ho Chi Minh", 800, 20000, "HCM light")]
    [InlineData("Ho Chi Minh", 1000, 20000, "HCM light")]
    [InlineData("Ho Chi Minh", 2500, 30000, "HCM heavy")]
    [InlineData("Ha Noi", 800, 45000, "Everywhere else")]
    public void The_first_matching_row_by_priority_sets_the_fee(string province, int grams, decimal fee, string row)
    {
        var result = ShippingFeePolicy.Compute(Settings(40000, 0), PriceList, province, grams, 100000);
        (result.Fee, result.Rule!.Name).Should().Be((fee, row));
    }

    [Fact]
    public void A_lower_priority_number_wins_even_when_a_broader_row_also_matches()
    {
        var rows = new List<ShippingFeeRule> { Rule("broad", 50, 99000), Rule("narrow", 5, 11000, "Da Nang") };
        ShippingFeePolicy.Compute(Settings(0, 0), rows, "Da Nang", 100, 1).Fee.Should().Be(11000);
    }

    [Fact]
    public void A_parcel_no_row_matches_pays_the_flat_fee()
    {
        var rows = new List<ShippingFeeRule> { Rule("HCM", 1, 20000, "Ho Chi Minh") };
        var result = ShippingFeePolicy.Compute(Settings(40000, 0), rows, "Hue", 500, 1);
        (result.Fee, result.Rule).Should().Be((40000m, (ShippingFeeRule?)null));
    }

    [Fact]
    public void Without_rows_and_flat_fee_the_fee_is_zero() =>
        ShippingFeePolicy.Compute(Settings(0, 0), new List<ShippingFeeRule>(), "Hue", 500, 1).Fee.Should().Be(0m);

    [Fact]
    public void A_shop_that_only_has_rows_charges_by_them_even_with_a_zero_flat_fee() =>
        ShippingFeePolicy.Compute(Settings(0, 0), PriceList, "Ho Chi Minh", 500, 1).Fee.Should().Be(20000);

    [Fact]
    public void The_free_delivery_threshold_applies_on_top_of_a_row()
    {
        var below = ShippingFeePolicy.Compute(Settings(40000, 500000), PriceList, "Ho Chi Minh", 500, 499999);
        var at = ShippingFeePolicy.Compute(Settings(40000, 500000), PriceList, "Ho Chi Minh", 500, 500000);

        below.Fee.Should().Be(20000);
        (at.Fee, at.FreeBecauseOfThreshold).Should().Be((0m, true));
    }

    [Fact]
    public void A_free_row_is_not_reported_as_free_because_of_the_threshold()
    {
        var rows = new List<ShippingFeeRule> { Rule("pickup in store", 1, 0, "Ho Chi Minh") };
        var result = ShippingFeePolicy.Compute(Settings(40000, 500000), rows, "Ho Chi Minh", 500, 1000000);
        (result.Fee, result.FreeBecauseOfThreshold).Should().Be((0m, false));
    }

    // ── the service: products, weight, province ───────────────────────────

    private static ShippingFeeService Service(TenantShippingSettings? settings, List<ShippingFeeRule> rows, IReadOnlyDictionary<Guid, int>? weights = null, bool productDown = false)
    {
        var config = new Mock<IShippingConfigRepository>();
        config.Setup(c => c.GetTenantSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        config.Setup(c => c.GetFeeRulesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        var uow = new Mock<IShippingUnitOfWork>();
        uow.SetupGet(u => u.Config).Returns(config.Object);
        var client = new Mock<IProductWeightClient>();
        // An unreachable Product service answers with an empty list (the client swallows the failure), see ProductWeightClient.
        client.Setup(w => w.GetWeightsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(productDown ? new Dictionary<Guid, int>() : weights ?? new Dictionary<Guid, int>());
        return new ShippingFeeService(uow.Object, client.Object);
    }

    [Fact]
    public async Task The_parcel_weight_is_the_sum_of_the_products_with_the_default_for_one_without()
    {
        var shirt = Guid.NewGuid();
        var mug = Guid.NewGuid();
        var settings = Settings(40000, 0);                              // default item weight 500 g
        var service = Service(settings, PriceList, new Dictionary<Guid, int> { [shirt] = 400 });

        var quote = await service.QuoteAsync(new ShippingFeeRequest
        {
            MerchandiseValue = 100000, Province = "Ho Chi Minh",
            Items = { new ShippingFeeItem(shirt, 2), new ShippingFeeItem(mug, 1) },    // 800 + 500 = 1300 g
        }, default);

        (quote.WeightGrams, quote.WeightEstimated, quote.Fee, quote.RuleName).Should().Be((1300, true, 30000m, "HCM heavy"));
    }

    [Fact]
    public async Task When_every_product_has_a_weight_it_is_not_an_estimate()
    {
        var shirt = Guid.NewGuid();
        var quote = await Service(Settings(40000, 0), PriceList, new Dictionary<Guid, int> { [shirt] = 300 })
            .QuoteAsync(new ShippingFeeRequest { MerchandiseValue = 1, Province = "Ho Chi Minh", Items = { new ShippingFeeItem(shirt, 2) } }, default);

        (quote.WeightGrams, quote.WeightEstimated, quote.Fee).Should().Be((600, false, 20000m));
    }

    [Fact]
    public async Task When_Product_cannot_be_reached_the_default_weight_is_used_and_flagged()
    {
        var quote = await Service(Settings(40000, 0), PriceList, productDown: true)
            .QuoteAsync(new ShippingFeeRequest { MerchandiseValue = 1, Province = "Ho Chi Minh", Items = { new ShippingFeeItem(Guid.NewGuid(), 4) } }, default);   // 4 x 500 g

        (quote.WeightGrams, quote.WeightEstimated, quote.Fee).Should().Be((2000, true, 30000m));
    }

    [Fact]
    public async Task A_caller_that_gives_no_products_and_no_province_gets_the_flat_fee_as_before()
    {
        var quote = await Service(Settings(40000, 500000), new List<ShippingFeeRule>()).QuoteAsync(100000m, default);

        (quote.Fee, quote.IsFree, quote.FreeShippingThreshold, quote.RuleName).Should().Be((40000m, false, 500000m, (string?)null));
    }

    [Fact]
    public async Task A_quantity_of_zero_or_a_negative_value_is_refused()
    {
        var service = Service(Settings(40000, 0), PriceList);
        await ((Func<Task>)(() => service.QuoteAsync(new ShippingFeeRequest { MerchandiseValue = 1, Items = { new ShippingFeeItem(Guid.NewGuid(), 0) } }, default)))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await ((Func<Task>)(() => service.QuoteAsync(-1m, default))).Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
