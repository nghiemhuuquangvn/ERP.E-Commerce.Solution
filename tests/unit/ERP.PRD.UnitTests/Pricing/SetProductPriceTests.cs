using ERP.PRD.Application.Features.Pricing.Commands;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
using ERP.PRD.Domain.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ERP.PRD.UnitTests.Pricing;

/// <summary>R111: "Set price" writes the shop's retail price list (not the variant's cost) and tells POS / Sales when the product's price moved.</summary>
public class SetProductPriceTests
{
    private readonly PriceHarness _h = new(cost: 40m);

    private SetProductPriceHandler Handler() => new(_h.Uow.Object, _h.Tracker(), Mock.Of<ILogger<SetProductPriceHandler>>());

    private Task Set(decimal basePrice, decimal? discounted = null)
        => Handler().Handle(new SetProductPriceCommand { VariantId = _h.Variant.Id, BasePrice = basePrice, DiscountedPrice = discounted }, CancellationToken.None);

    private List<ProductPriceChangedDomainEvent> Events() => _h.Product.DomainEvents.OfType<ProductPriceChangedDomainEvent>().ToList();

    [Fact]
    public async Task The_first_price_creates_the_default_retail_list_and_leaves_the_cost_alone()
    {
        await Set(120m);

        _h.PriceLists.Verify(p => p.AddAsync(It.Is<PriceList>(l => l.Name == PriceList.DefaultRetailName && l.Channel == PriceListChannel.Retail && l.IsActive), It.IsAny<CancellationToken>()), Times.Once);
        var item = _h.DefaultList!.Items.Should().ContainSingle().Subject;
        item.VariantId.Should().Be(_h.Variant.Id);
        item.BasePrice.Should().Be(120m);
        item.MinQuantity.Should().Be(1);
        _h.Variant.Cost.Should().Be(40m);
    }

    [Fact]
    public async Task The_first_price_raises_one_event_from_zero_to_the_new_price()
    {
        await Set(120m);

        var evt = Events().Should().ContainSingle().Subject;
        evt.ProductId.Should().Be(_h.Product.Id);
        evt.VariantId.Should().Be(_h.Variant.Id);
        evt.OldPrice.Should().Be(0m);
        evt.NewPrice.Should().Be(120m);
    }

    [Fact]
    public async Task A_second_price_updates_the_same_row_and_reports_old_and_new()
    {
        await Set(120m);
        _h.Product.ClearDomainEvents();

        await Set(150m);

        _h.PriceLists.Verify(p => p.AddAsync(It.IsAny<PriceList>(), It.IsAny<CancellationToken>()), Times.Once);   // still only the first call
        _h.PriceLists.Verify(p => p.MarkItemAdded(It.IsAny<PriceListItem>()), Times.Never);
        _h.DefaultList!.Items.Should().ContainSingle().Which.BasePrice.Should().Be(150m);
        var evt = Events().Should().ContainSingle().Subject;
        evt.OldPrice.Should().Be(120m);
        evt.NewPrice.Should().Be(150m);
    }

    [Fact]
    public async Task Setting_the_same_price_again_raises_no_event()
    {
        await Set(120m);
        _h.Product.ClearDomainEvents();

        await Set(120m);

        Events().Should().BeEmpty();
    }

    [Fact]
    public async Task The_discounted_price_is_what_the_product_sells_for()
    {
        await Set(120m, discounted: 99m);

        Events().Should().ContainSingle().Which.NewPrice.Should().Be(99m);
        _h.DefaultList!.Items.Single().DiscountedPrice.Should().Be(99m);
    }

    [Fact]
    public async Task A_variant_missing_from_an_existing_default_list_is_added_as_a_new_row()
    {
        var other = Guid.NewGuid();
        _h.ExistingDefaultList((other, 10m));

        await Set(120m);

        _h.PriceLists.Verify(p => p.MarkItemAdded(It.Is<PriceListItem>(i => i.VariantId == _h.Variant.Id)), Times.Once);
        _h.PriceLists.Verify(p => p.AddAsync(It.IsAny<PriceList>(), It.IsAny<CancellationToken>()), Times.Never);
        _h.DefaultList!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task An_unknown_variant_is_a_not_found()
    {
        var act = () => Handler().Handle(new SetProductPriceCommand { VariantId = Guid.NewGuid(), BasePrice = 10m }, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task A_failed_save_rolls_the_transaction_back_and_raises_the_error()
    {
        _h.Uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db down"));

        var act = () => Set(120m);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _h.Uow.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _h.Uow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void The_validator_refuses_a_discount_that_is_not_below_the_base_price()
    {
        var result = new SetProductPriceValidator().Validate(new SetProductPriceCommand { VariantId = Guid.NewGuid(), BasePrice = 100m, DiscountedPrice = 100m });

        result.IsValid.Should().BeFalse();
    }
}
