using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace ERP.PRD.UnitTests.Pricing;

/// <summary>R111: the one rule that decides what a variant / product sells for at retail.</summary>
public class RetailPriceRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Variant = Guid.NewGuid();

    private static PriceCandidate Row(
        PriceListChannel channel = PriceListChannel.Retail, decimal basePrice = 100m, decimal? discounted = null, int minQty = 1,
        bool active = true, int effectiveDaysAgo = 10, int? expiresInDays = null) =>
        new(Variant, channel, active, Now.AddDays(-effectiveDaysAgo), expiresInDays == null ? null : Now.AddDays(expiresInDays.Value), minQty, basePrice, discounted);

    [Fact]
    public void A_single_current_retail_row_is_the_price()
        => RetailPriceRules.PickVariantPrice(new[] { Row(basePrice: 120m) }, Now).Should().Be(120m);

    [Fact]
    public void The_discounted_price_wins_over_the_base_price()
        => RetailPriceRules.PickVariantPrice(new[] { Row(basePrice: 120m, discounted: 99m) }, Now).Should().Be(99m);

    [Fact]
    public void No_rows_means_no_price()
        => RetailPriceRules.PickVariantPrice(Array.Empty<PriceCandidate>(), Now).Should().BeNull();

    [Fact]
    public void A_list_that_is_inactive_expired_or_not_yet_effective_is_ignored()
    {
        RetailPriceRules.PickVariantPrice(new[] { Row(active: false) }, Now).Should().BeNull();
        RetailPriceRules.PickVariantPrice(new[] { Row(expiresInDays: -1) }, Now).Should().BeNull();
        RetailPriceRules.PickVariantPrice(new[] { Row(effectiveDaysAgo: -5) }, Now).Should().BeNull();
    }

    [Fact]
    public void A_list_that_expires_in_the_future_still_counts()
        => RetailPriceRules.PickVariantPrice(new[] { Row(basePrice: 80m, expiresInDays: 3) }, Now).Should().Be(80m);

    [Fact]
    public void A_wholesale_list_is_never_a_retail_price()
        => RetailPriceRules.PickVariantPrice(new[] { Row(PriceListChannel.Wholesale, 50m) }, Now).Should().BeNull();

    [Fact]
    public void Retail_wins_over_Ecommerce_even_when_the_Ecommerce_list_is_newer()
        => RetailPriceRules.PickVariantPrice(new[]
        {
            Row(PriceListChannel.Ecommerce, 90m, effectiveDaysAgo: 1),
            Row(PriceListChannel.Retail, 100m, effectiveDaysAgo: 30)
        }, Now).Should().Be(100m);

    [Fact]
    public void Ecommerce_is_used_when_there_is_no_retail_row()
        => RetailPriceRules.PickVariantPrice(new[]
        {
            Row(PriceListChannel.Wholesale, 50m),
            Row(PriceListChannel.Ecommerce, 90m)
        }, Now).Should().Be(90m);

    [Fact]
    public void The_newest_list_of_the_same_channel_wins()
        => RetailPriceRules.PickVariantPrice(new[]
        {
            Row(basePrice: 100m, effectiveDaysAgo: 30),
            Row(basePrice: 110m, effectiveDaysAgo: 2)
        }, Now).Should().Be(110m);

    [Fact]
    public void Within_one_list_the_lowest_quantity_tier_is_the_price()
        => RetailPriceRules.PickVariantPrice(new[]
        {
            Row(basePrice: 70m, minQty: 10),
            Row(basePrice: 100m, minQty: 1)
        }, Now).Should().Be(100m);

    [Fact]
    public void A_product_price_is_its_cheapest_priced_variant()
        => RetailPriceRules.ProductPrice(new decimal?[] { 150m, null, 99m }).Should().Be(99m);

    [Fact]
    public void A_product_with_no_priced_variant_has_no_price()
        => RetailPriceRules.ProductPrice(new decimal?[] { null, null }).Should().BeNull();
}
