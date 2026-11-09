using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Messaging;
using ERP.SO.Application.Commands;
using ERP.SO.Application.DTOs;
using ERP.SO.Application.Handlers;
using ERP.SO.Application.Interfaces;
using ERP.SO.Application.Sagas;
using ERP.SO.Application.Services;
using ERP.SO.Domain.Entities;
using ERP.SO.Domain.Sagas;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.SO.UnitTests.Orders;

/// <summary>R113: the server prices every order / quotation line; a client's price is only a claim that must equal the system price unless the caller may override.</summary>
public class ServerPricingTests
{
    private static readonly Guid Shirt = Guid.NewGuid();
    private static readonly Guid Hat = Guid.NewGuid();

    private readonly Mock<IProductPriceClient> _prices = new();

    private LinePricer Pricer(params (Guid Product, decimal Price)[] system)
    {
        _prices.Setup(p => p.GetPricesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(system.ToDictionary(s => s.Product, s => s.Price));
        return new LinePricer(_prices.Object);
    }

    private static LineToPrice Line(Guid product, decimal? clientPrice, string name = "Shirt") => new(product, name, clientPrice);

    // ── the rule ──────────────────────────────────────────────────────────

    [Fact]
    public async Task A_line_without_a_price_gets_the_system_price()
    {
        var result = await Pricer((Shirt, 25m)).PriceAsync(new[] { Line(Shirt, null) }, allowOverride: false);

        result.Should().ContainSingle().Which.Should().Be(new PricedLine(25m, 25m, false));
    }

    [Fact]
    public async Task The_same_price_is_accepted_to_the_cent()
    {
        var result = await Pricer((Shirt, 25m)).PriceAsync(new[] { Line(Shirt, 25.004m) }, allowOverride: false);

        result.Single().UnitPrice.Should().Be(25m);
        result.Single().IsManual.Should().BeFalse();
    }

    [Fact]
    public async Task A_customer_whose_price_is_out_of_date_is_refused_with_both_prices_and_nothing_is_priced()
    {
        var act = () => Pricer((Shirt, 25m)).PriceAsync(new[] { Line(Shirt, 20m) }, allowOverride: false);

        var error = await act.Should().ThrowAsync<BusinessRuleException>();
        error.Which.Message.Should().Contain("Shirt").And.Contain("20.00").And.Contain("25.00");
    }

    [Fact]
    public async Task A_price_of_one_cent_is_refused_like_any_other_difference()
    {
        var act = () => Pricer((Shirt, 25m)).PriceAsync(new[] { Line(Shirt, 0.01m) }, allowOverride: false);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task A_caller_with_the_override_permission_books_the_price_they_typed_as_a_manual_price()
    {
        var result = await Pricer((Shirt, 25m)).PriceAsync(new[] { Line(Shirt, 20m) }, allowOverride: true);

        result.Single().Should().Be(new PricedLine(20m, 25m, true));
    }

    [Fact]
    public async Task A_product_without_a_system_price_cannot_be_ordered()
    {
        var act = () => Pricer((Shirt, 25m)).PriceAsync(new[] { Line(Hat, 10m, "Hat") }, allowOverride: false);

        var error = await act.Should().ThrowAsync<BusinessRuleException>();
        error.Which.Message.Should().Contain("Hat").And.Contain("no price");
    }

    [Fact]
    public async Task A_product_with_a_zero_system_price_counts_as_having_no_price()
    {
        var act = () => Pricer((Hat, 0m)).PriceAsync(new[] { Line(Hat, null, "Hat") }, allowOverride: false);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Someone_who_may_override_can_price_a_product_that_has_no_system_price()
    {
        var result = await Pricer().PriceAsync(new[] { Line(Hat, 10m, "Hat") }, allowOverride: true);

        result.Single().Should().Be(new PricedLine(10m, null, true));
    }

    [Fact]
    public async Task Someone_who_may_override_still_needs_to_type_a_price_for_an_unpriced_product()
    {
        var act = () => Pricer().PriceAsync(new[] { Line(Hat, null, "Hat") }, allowOverride: true);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task When_ProductService_cannot_answer_the_order_is_refused_not_priced_by_guess()
    {
        _prices.Setup(p => p.GetPricesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, decimal>?)null);

        var act = () => new LinePricer(_prices.Object).PriceAsync(new[] { Line(Shirt, 25m) }, allowOverride: true);

        var error = await act.Should().ThrowAsync<BusinessRuleException>();
        error.Which.Message.Should().Contain("try again");
    }

    [Fact]
    public async Task Several_lines_are_priced_with_one_call_and_a_repeated_product_is_asked_once()
    {
        var pricer = Pricer((Shirt, 25m), (Hat, 10m));

        var result = await pricer.PriceAsync(new[] { Line(Shirt, null), Line(Hat, 10m, "Hat"), Line(Shirt, null) }, allowOverride: false);

        result.Select(r => r.UnitPrice).Should().Equal(25m, 10m, 25m);
        _prices.Verify(p => p.GetPricesAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── the order handler ─────────────────────────────────────────────────

    private readonly Mock<ISalesOrderUnitOfWork> _uow = new();
    private readonly Mock<ISalesOrderRepository> _orders = new();
    private readonly Mock<IOrderFulfillmentSagaRepository> _sagas = new();
    private readonly Mock<IPromotionServiceClient> _promotions = new();
    private readonly Mock<IWarehouseServiceClient> _warehouse = new();
    private readonly Mock<IShippingServiceClient> _shipping = new();
    private readonly Mock<IMessageBus> _bus = new();
    private SalesOrder? _saved;

    private CreateOrderCommandHandler OrderHandler(LinePricer pricer)
    {
        _uow.SetupGet(u => u.Orders).Returns(_orders.Object);
        _uow.SetupGet(u => u.Sagas).Returns(_sagas.Object);
        _orders.Setup(o => o.AddAsync(It.IsAny<SalesOrder>(), It.IsAny<CancellationToken>()))
            .Callback<SalesOrder, CancellationToken>((o, _) => _saved = o).Returns(Task.CompletedTask);
        _warehouse.Setup(w => w.CheckAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockAvailabilityResult { IsAvailable = true, AvailableQuantity = 100 });
        _shipping.Setup(s => s.GetFeeAsync(It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<IReadOnlyCollection<(Guid ProductId, int Quantity)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _bus.Setup(b => b.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var tenant = Guid.NewGuid();
        var user = new Mock<IUserContextService>();
        user.Setup(u => u.GetCurrentUser()).Returns(new UserContext { UserId = "staff-1", TenantId = tenant.ToString() });
        var numbers = new Mock<IOrderNumberGenerator>();
        numbers.Setup(n => n.NextAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("SO-1");
        var saga = new OrderFulfillmentSaga(_uow.Object, _bus.Object, new Mock<IMediator>().Object, NullLogger<OrderFulfillmentSaga>.Instance);
        return new CreateOrderCommandHandler(_uow.Object, _promotions.Object, _warehouse.Object, _shipping.Object, pricer, saga, user.Object, numbers.Object);
    }

    private static CreateOrderCommand Order(decimal? clientPrice, bool allowOverride = false) => new()
    {
        CustomerId = Guid.NewGuid(),
        CustomerName = "An",
        CustomerEmail = "an@example.com",
        PaymentMethod = "COD",
        AllowPriceOverride = allowOverride,
        ShippingAddress = new AddressDto { Street = "1 Le Loi", Ward = "Ben Nghe", City = "Quan 1", State = "Ho Chi Minh", Country = "Vietnam" },
        LineItems = new List<OrderLineItemInput> { new() { ProductId = Shirt, ProductName = "Shirt", Quantity = 2, UnitPrice = clientPrice } },
    };

    [Fact]
    public async Task An_order_is_booked_at_the_system_price_when_the_client_sends_none()
    {
        var reply = await OrderHandler(Pricer((Shirt, 25m))).Handle(Order(null), CancellationToken.None);

        _saved!.LineItems.Single().UnitPrice.Should().Be(25m);
        reply.TotalAmount.Should().Be(50m);
    }

    [Fact]
    public async Task A_customer_cannot_order_at_a_price_of_their_own_and_nothing_is_saved()
    {
        var act = () => OrderHandler(Pricer((Shirt, 25m))).Handle(Order(0.01m), CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
        _orders.Verify(o => o.AddAsync(It.IsAny<SalesOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        _warehouse.Verify(w => w.CheckAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_manual_price_is_booked_and_noted_on_the_order_with_who_set_it()
    {
        await OrderHandler(Pricer((Shirt, 25m))).Handle(Order(20m, allowOverride: true), CancellationToken.None);

        _saved!.LineItems.Single().UnitPrice.Should().Be(20m);
        var note = _saved.InternalNotes.Should().ContainSingle().Subject;
        note.Note.Should().Contain("Manual price").And.Contain("25.00").And.Contain("20.00");
        note.AddedBy.Should().Be("price-override:staff-1");
    }

    [Fact]
    public async Task The_shipping_fee_is_asked_on_the_system_priced_goods()
    {
        await OrderHandler(Pricer((Shirt, 25m))).Handle(Order(null), CancellationToken.None);

        _shipping.Verify(s => s.GetFeeAsync(50m, It.IsAny<string?>(), It.IsAny<IReadOnlyCollection<(Guid ProductId, int Quantity)>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void An_order_request_may_leave_the_price_out_but_not_send_zero()
    {
        var validator = new ERP.SO.Application.Validators.CreateOrderCommandValidator();

        validator.Validate(Order(null)).Errors.Should().NotContain(e => e.PropertyName.Contains("UnitPrice"));
        validator.Validate(Order(0m)).Errors.Should().Contain(e => e.PropertyName.Contains("UnitPrice"));
    }
}
