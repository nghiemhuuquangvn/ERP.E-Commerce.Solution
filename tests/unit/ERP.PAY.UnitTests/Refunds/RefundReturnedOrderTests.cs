using ERP.PAY.Application.Commands;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Domain.Entities;
using ERP.PAY.Domain.Enums;
using ERP.PAY.Infrastructure.EventBus;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using Xunit;

namespace ERP.PAY.UnitTests.Refunds;

/// <summary>ROADMAP P1-5: a completed return refunds the customer on the order's payment, once, never above what is refundable.</summary>
public class RefundReturnedOrderTests
{
    private readonly Mock<IPaymentUnitOfWork> _uow = new();
    private readonly Mock<IPaymentRepository> _payments = new();
    private readonly Mock<IMediator> _mediator = new();

    public RefundReturnedOrderTests() => _uow.SetupGet(u => u.Payments).Returns(_payments.Object);

    private RefundReturnedOrderCommandHandler Handler() =>
        new(_uow.Object, _mediator.Object, NullLogger<RefundReturnedOrderCommandHandler>.Instance);

    private Payment Captured(decimal amount = 1000m)
    {
        var payment = Payment.Initiate(Guid.NewGuid(), amount, PaymentMethod.CreditCard, "VND");
        payment.Authorize("GW-1", "VNPay");
        payment.Capture();
        _payments.Setup(p => p.GetByOrderIdAsync(payment.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        return payment;
    }

    [Fact]
    public async Task The_return_is_refunded_through_the_existing_refund_flow_with_a_reason_that_names_the_order()
    {
        var payment = Captured();

        var result = await Handler().Handle(new RefundReturnedOrderCommand { OrderId = payment.OrderId, RefundAmount = 600m }, default);

        result.Should().Be(RefundReturnedOrderResult.Requested);
        _mediator.Verify(m => m.Send(It.Is<RefundPaymentCommand>(c =>
            c.PaymentId == payment.Id && c.Amount == 600m && c.Reason == RefundReturnedOrderCommandHandler.ReasonFor(payment.OrderId)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task The_amount_is_capped_at_what_is_still_refundable()
    {
        var payment = Captured(1000m);
        var earlier = payment.AddRefund(300m, "goodwill");
        payment.CompleteRefund(earlier, "GW-R1");

        await Handler().Handle(new RefundReturnedOrderCommand { OrderId = payment.OrderId, RefundAmount = 900m }, default);

        _mediator.Verify(m => m.Send(It.Is<RefundPaymentCommand>(c => c.Amount == 700m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_redelivered_event_does_not_refund_twice_but_a_declined_refund_is_tried_again()
    {
        var payment = Captured();
        var reason = RefundReturnedOrderCommandHandler.ReasonFor(payment.OrderId);
        var first = payment.AddRefund(600m, reason);

        var pending = await Handler().Handle(new RefundReturnedOrderCommand { OrderId = payment.OrderId, RefundAmount = 600m }, default);
        pending.Should().Be(RefundReturnedOrderResult.AlreadyDone);

        first.MarkAsFailed("declined");
        payment.ReconcileStatusWithConfirmedRefunds();
        var again = await Handler().Handle(new RefundReturnedOrderCommand { OrderId = payment.OrderId, RefundAmount = 600m }, default);

        again.Should().Be(RefundReturnedOrderResult.Requested);
        _mediator.Verify(m => m.Send(It.IsAny<RefundPaymentCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task An_order_without_a_payment_or_with_a_payment_that_cannot_be_refunded_is_left_alone()
    {
        var none = await Handler().Handle(new RefundReturnedOrderCommand { OrderId = Guid.NewGuid(), RefundAmount = 10m }, default);
        none.Should().Be(RefundReturnedOrderResult.NoPayment);

        var pendingPayment = Payment.Initiate(Guid.NewGuid(), 100m, PaymentMethod.CreditCard, "VND");
        _payments.Setup(p => p.GetByOrderIdAsync(pendingPayment.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(pendingPayment);
        var notCaptured = await Handler().Handle(new RefundReturnedOrderCommand { OrderId = pendingPayment.OrderId, RefundAmount = 10m }, default);
        notCaptured.Should().Be(RefundReturnedOrderResult.NotRefundable);

        var full = Captured(100m);
        full.CompleteRefund(full.AddRefund(100m, "all"), "GW-R");
        (await Handler().Handle(new RefundReturnedOrderCommand { OrderId = full.OrderId, RefundAmount = 50m }, default)).Should().Be(RefundReturnedOrderResult.NotRefundable);

        _mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public void The_event_of_Sales_is_read_by_Payment()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var json = $"{{\"OrderId\":\"{orderId}\",\"TenantId\":\"{tenantId}\",\"RefundAmount\":250.5,\"Lines\":[]}}";

        var contract = JsonSerializer.Deserialize<SalesOrderReturnedContract>(json)!;

        (contract.OrderId, contract.TenantId, contract.RefundAmount).Should().Be((orderId, tenantId, 250.5m));
    }
}
