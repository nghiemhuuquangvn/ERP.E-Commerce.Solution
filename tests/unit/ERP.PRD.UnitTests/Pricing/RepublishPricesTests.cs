using ERP.PRD.Application.Features.Pricing.Commands;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
using ERP.PRD.Domain.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ERP.PRD.UnitTests.Pricing;

/// <summary>R111: republish re-sends today's retail price (old = new) so a POS cache that never saw it catches up.</summary>
public class RepublishPricesTests
{
    private readonly PriceHarness _h = new();

    private RepublishPricesHandler Handler() => new(_h.Uow.Object, _h.Resolver, Mock.Of<ILogger<RepublishPricesHandler>>());

    private List<ProductPriceChangedDomainEvent> Events(Product p) => p.DomainEvents.OfType<ProductPriceChangedDomainEvent>().ToList();

    [Fact]
    public async Task A_priced_product_gets_one_event_with_the_same_old_and_new_price()
    {
        _h.ExistingDefaultList((_h.Variant.Id, 120m));
        _h.Products.Setup(p => p.GetAllActiveIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Guid> { _h.Product.Id });

        var result = await Handler().Handle(new RepublishPricesCommand(), CancellationToken.None);

        result.ProductsChecked.Should().Be(1);
        result.EventsRaised.Should().Be(1);
        var evt = Events(_h.Product).Should().ContainSingle().Subject;
        evt.OldPrice.Should().Be(120m);
        evt.NewPrice.Should().Be(120m);
        evt.VariantId.Should().Be(_h.Variant.Id);
    }

    [Fact]
    public async Task A_product_with_no_price_is_skipped()
    {
        _h.Products.Setup(p => p.GetAllActiveIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Guid> { _h.Product.Id });

        var result = await Handler().Handle(new RepublishPricesCommand(), CancellationToken.None);

        result.ProductsChecked.Should().Be(1);
        result.EventsRaised.Should().Be(0);
        Events(_h.Product).Should().BeEmpty();
    }

    [Fact]
    public async Task A_wholesale_only_product_is_skipped_too()
    {
        var wholesale = PriceList.Create("Wholesale", DateTime.UtcNow.AddDays(-5), channel: PriceListChannel.Wholesale);
        wholesale.AddItem(_h.Variant.Id, 60m);
        _h.Resolver.Lists.Add(wholesale);
        _h.Products.Setup(p => p.GetAllActiveIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Guid> { _h.Product.Id });

        var result = await Handler().Handle(new RepublishPricesCommand(), CancellationToken.None);

        result.EventsRaised.Should().Be(0);
    }
}
