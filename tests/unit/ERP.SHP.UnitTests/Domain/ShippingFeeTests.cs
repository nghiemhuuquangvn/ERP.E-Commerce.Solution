using ERP.SHP.Application.Interfaces;
using ERP.SHP.Application.Services;
using ERP.SHP.Domain.Entities.Settings;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.SHP.UnitTests.Domain;

/// <summary>ROADMAP P1-11b: the customer pays the shop's flat fee, free once the goods reach the shop's threshold.</summary>
public class ShippingFeeTests
{
    [Theory]
    [InlineData(30000, 500000, 100000, 30000)]   // below the threshold
    [InlineData(30000, 500000, 499999, 30000)]   // just below
    [InlineData(30000, 500000, 500000, 0)]       // at the threshold: free
    [InlineData(30000, 500000, 900000, 0)]       // above
    [InlineData(30000, 0, 9000000, 30000)]       // threshold 0 = never free
    [InlineData(0, 500000, 100000, 0)]           // the shop does not charge
    [InlineData(-5, 0, 100, 0)]                  // a nonsense fee is never charged
    public void The_fee_follows_the_shop_settings(decimal fee, decimal threshold, decimal value, decimal expected) =>
        ShippingFeePolicy.Compute(fee, threshold, value).Should().Be(expected);

    [Fact]
    public void A_shop_without_settings_charges_nothing() =>
        ShippingFeePolicy.Compute(null, 100000m).Should().Be(0m);

    private static ShippingFeeService ServiceFor(TenantShippingSettings? settings)
    {
        var config = new Mock<IShippingConfigRepository>();
        config.Setup(c => c.GetTenantSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);
        var uow = new Mock<IShippingUnitOfWork>();
        uow.SetupGet(u => u.Config).Returns(config.Object);
        return new ShippingFeeService(uow.Object);
    }

    private static TenantShippingSettings Settings(decimal fee, decimal threshold)
    {
        var s = TenantShippingSettings.CreateDefault(Guid.NewGuid());
        s.Update(fee, threshold);
        return s;
    }

    [Fact]
    public async Task The_quote_says_when_delivery_is_free_because_of_the_threshold()
    {
        var svc = ServiceFor(Settings(30000, 500000));

        var below = await svc.QuoteAsync(100000, CancellationToken.None);
        below.Fee.Should().Be(30000);
        below.IsFree.Should().BeFalse();
        below.FreeShippingThreshold.Should().Be(500000);

        var above = await svc.QuoteAsync(600000, CancellationToken.None);
        above.Fee.Should().Be(0);
        above.IsFree.Should().BeTrue();
    }

    [Fact]
    public async Task A_shop_that_never_charges_is_not_reported_as_free_shipping()
    {
        var quote = await ServiceFor(Settings(0, 0)).QuoteAsync(100000, CancellationToken.None);
        quote.Fee.Should().Be(0);
        quote.IsFree.Should().BeFalse();
    }

    [Fact]
    public async Task A_negative_value_is_refused()
    {
        var act = () => ServiceFor(Settings(30000, 0)).QuoteAsync(-1, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
