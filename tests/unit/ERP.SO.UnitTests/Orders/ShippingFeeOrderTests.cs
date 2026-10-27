using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Messaging;
using ERP.SO.Application.Commands;
using ERP.SO.Application.DTOs;
using ERP.SO.Application.Handlers;
using ERP.SO.Application.Interfaces;
using ERP.SO.Application.Sagas;
using ERP.SO.Domain.Entities;
using ERP.SO.Domain.Events;
using ERP.SO.Domain.Sagas;
using ERP.SO.Domain.ValueObjects;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.SO.UnitTests.Orders;

/// <summary>ROADMAP P1-11b: the customer pays the shop's delivery fee, decided by ShippingService, and the order, its event and the COD cash all carry it.</summary>
public class ShippingFeeOrderTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static SalesOrder Order(decimal unitPrice = 100_000m, int quantity = 2, string? payment = "COD") =>
        SalesOrder.Create(Guid.NewGuid(), "An", "an@example.com", new Address("1 Le Loi", "Quan 1", "Ho Chi Minh", "700000", "Vietnam", "Phuong Ben Nghe"),
            new List<OrderLineItem> { new() { ProductId = Guid.NewGuid(), ProductName = "Shirt", Quantity = quantity, UnitPrice = unitPrice } },
            orderNo: "SO-1", paymentMethod: payment);

    // ── the order ─────────────────────────────────────────────────────────

    [Fact]
    public void The_fee_is_part_of_the_total_and_the_goods_stay_separate()
    {
        var order = Order();                    // goods 200 000

        order.SetShippingFee(30_000m);

        order.ShippingFee.Should().Be(30_000m);
        order.MerchandiseTotal.Should().Be(200_000m);
        order.TotalAmount.Should().Be(230_000m);
    }

    [Fact]
    public void A_coupon_reduces_the_goods_never_the_delivery_fee_in_either_order_of_calls()
    {
        var feeFirst = Order();
        feeFirst.SetShippingFee(30_000m);
        feeFirst.ApplyDiscount(Guid.NewGuid(), 50_000m);

        var discountFirst = Order();
        discountFirst.ApplyDiscount(Guid.NewGuid(), 50_000m);
        discountFirst.SetShippingFee(30_000m);

        feeFirst.TotalAmount.Should().Be(180_000m);
        discountFirst.TotalAmount.Should().Be(180_000m);
        discountFirst.MerchandiseTotal.Should().Be(150_000m);
    }

    [Fact]
    public void A_coupon_larger_than_the_goods_leaves_only_the_delivery_fee()
    {
        var order = Order();
        order.ApplyDiscount(Guid.NewGuid(), 999_000m);
        order.SetShippingFee(30_000m);

        order.TotalAmount.Should().Be(30_000m);
    }

    [Fact]
    public void A_negative_fee_is_refused()
    {
        var act = () => Order().SetShippingFee(-1m);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void The_order_created_event_carries_the_final_total_the_fee_and_the_discount()
    {
        var order = Order();
        order.ApplyDiscount(Guid.NewGuid(), 50_000m);
        order.SetShippingFee(30_000m);

        var created = order.DomainEvents.OfType<SalesOrderCreatedEvent>().Single();
        created.TotalAmount.Should().Be(180_000m);          // what Accounting books and Payment collects
        created.ShippingFee.Should().Be(30_000m);
        created.DiscountAmount.Should().Be(50_000m);
    }

    [Fact]
    public void The_ward_is_kept_with_the_address()
    {
        Order().ShippingAddress.Ward.Should().Be("Phuong Ben Nghe");
        new Address("a", "b", "c", "d").Ward.Should().BeEmpty();
    }

    // ── creating the order ────────────────────────────────────────────────

    private readonly Mock<ISalesOrderUnitOfWork> _uow = new();
    private readonly Mock<ISalesOrderRepository> _orders = new();
    private readonly Mock<IOrderFulfillmentSagaRepository> _sagas = new();
    private readonly Mock<IPromotionServiceClient> _promotions = new();
    private readonly Mock<IWarehouseServiceClient> _warehouse = new();
    private readonly Mock<IShippingServiceClient> _shipping = new();
    private readonly Mock<IMessageBus> _bus = new();

    private CreateOrderCommandHandler Handler()
    {
        _uow.SetupGet(u => u.Orders).Returns(_orders.Object);
        _uow.SetupGet(u => u.Sagas).Returns(_sagas.Object);
        _warehouse.Setup(w => w.CheckAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockAvailabilityResult { IsAvailable = true, AvailableQuantity = 100 });
        _bus.Setup(b => b.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var user = new Mock<IUserContextService>();
        user.Setup(u => u.GetCurrentUser()).Returns(new UserContext { UserId = "u", TenantId = Tenant.ToString() });
        var numbers = new Mock<IOrderNumberGenerator>();
        numbers.Setup(n => n.NextAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("SO-1");
        var saga = new OrderFulfillmentSaga(_uow.Object, _bus.Object, new Mock<IMediator>().Object, NullLogger<OrderFulfillmentSaga>.Instance);
        return new CreateOrderCommandHandler(_uow.Object, _promotions.Object, _warehouse.Object, _shipping.Object, saga, user.Object, numbers.Object);
    }

    private static CreateOrderCommand Command(string? coupon = null) => new()
    {
        CustomerId = Guid.NewGuid(),
        CustomerName = "An",
        CustomerEmail = "an@example.com",
        CustomerPhone = "0901234567",
        PaymentMethod = "COD",
        CouponCode = coupon,
        ShippingAddress = new AddressDto { Street = "1 Le Loi", Ward = "Phuong Ben Nghe", City = "Quan 1", State = "Ho Chi Minh", Country = "Vietnam" },
        LineItems = new List<OrderLineItemDto> { new() { ProductId = Guid.NewGuid(), ProductName = "Shirt", Quantity = 2, UnitPrice = 100_000m } },
    };

    [Fact]
    public async Task The_fee_ShippingService_names_goes_into_the_saved_order_and_the_reply()
    {
        _shipping.Setup(s => s.GetFeeAsync(200_000m, It.IsAny<CancellationToken>())).ReturnsAsync(30_000m);
        SalesOrder? saved = null;
        _orders.Setup(o => o.AddAsync(It.IsAny<SalesOrder>(), It.IsAny<CancellationToken>())).Callback<SalesOrder, CancellationToken>((o, _) => saved = o).Returns(Task.CompletedTask);

        var dto = await Handler().Handle(Command(), default);

        saved!.ShippingFee.Should().Be(30_000m);
        saved.ShippingAddress.Ward.Should().Be("Phuong Ben Nghe");
        dto.ShippingFee.Should().Be(30_000m);
        dto.TotalAmount.Should().Be(230_000m);
        dto.ShippingAddress.Ward.Should().Be("Phuong Ben Nghe");
    }

    [Fact]
    public async Task The_fee_is_asked_for_the_goods_after_the_coupon_so_a_threshold_looks_at_what_is_really_paid()
    {
        _promotions.Setup(p => p.ValidateCouponAsync("SALE", 200_000m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CouponValidationResult { IsValid = true, DiscountAmount = 80_000m, PromotionId = Guid.NewGuid() });
        _shipping.Setup(s => s.GetFeeAsync(120_000m, It.IsAny<CancellationToken>())).ReturnsAsync(0m);

        var dto = await Handler().Handle(Command("SALE"), default);

        _shipping.Verify(s => s.GetFeeAsync(120_000m, It.IsAny<CancellationToken>()), Times.Once);
        dto.TotalAmount.Should().Be(120_000m);
        dto.ShippingFee.Should().Be(0m);
    }

    [Fact]
    public async Task When_the_fee_cannot_be_worked_out_the_order_is_refused_and_nothing_is_saved()
    {
        _shipping.Setup(s => s.GetFeeAsync(It.IsAny<decimal>(), It.IsAny<CancellationToken>())).ReturnsAsync((decimal?)null);

        var act = () => Handler().Handle(Command(), default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*delivery fee*");
        _orders.Verify(o => o.AddAsync(It.IsAny<SalesOrder>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
