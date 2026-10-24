using ERP.HRM.Application.Commands;
using ERP.HRM.Application.Handlers;
using ERP.HRM.Application.Interfaces;
using ERP.HRM.Domain.Entities;
using ERP.HRM.Domain.Enums;
using ERP.HRM.Domain.Events;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.HRM.UnitTests.Payroll;

/// <summary>ROADMAP P1-1d: a pay slip can be paid or cancelled once, and its events carry the figures the ledger posts.</summary>
public class PaySlipLifecycleTests
{
    private static PaySlip Slip(decimal gross = 5000m, decimal deductions = 1500m, decimal overtimePay = 100m) =>
        PaySlip.Create("PS-202609-1234", Guid.NewGuid(), 2026, 9, gross, deductions, 4m, overtimePay);

    [Fact]
    public void The_generated_event_carries_gross_overtime_deductions_and_the_period_end()
    {
        var evt = Slip().DomainEvents.OfType<PaySlipGeneratedEvent>().Single();

        evt.GrossSalary.Should().Be(5000m);
        evt.OvertimePay.Should().Be(100m);
        evt.Deductions.Should().Be(1500m);
        evt.NetPay.Should().Be(3600m);
        evt.NetPay.Should().Be(evt.GrossSalary + evt.OvertimePay - evt.Deductions);
        evt.PeriodEnd.Should().Be(new DateTime(2026, 9, 30));
        evt.PayPeriod.Should().Be("2026-09");
    }

    [Fact]
    public void Paying_a_pending_slip_records_the_date_and_raises_the_paid_event()
    {
        var slip = Slip();

        slip.MarkPaid(new DateTime(2026, 10, 5), "BankTransfer");

        slip.PaymentStatus.Should().Be((int)PaymentStatus.Paid);
        slip.PaymentDate.Should().Be(new DateTime(2026, 10, 5));
        var paid = slip.DomainEvents.OfType<PaySlipPaidEvent>().Single();
        paid.NetPay.Should().Be(3600m);
        paid.PaymentMethod.Should().Be("BankTransfer");
        paid.PaySlipId.Should().Be(slip.Id);
    }

    [Fact]
    public void A_slip_is_paid_only_once_and_a_paid_slip_cannot_be_cancelled()
    {
        var slip = Slip();
        slip.MarkPaid(DateTime.UtcNow);

        var payAgain = () => slip.MarkPaid(DateTime.UtcNow);
        var cancel = () => slip.Cancel("oops");

        payAgain.Should().Throw<InvalidOperationException>();
        cancel.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancelling_needs_a_reason_and_a_cancelled_slip_cannot_be_paid()
    {
        var slip = Slip();

        var noReason = () => slip.Cancel("  ");
        noReason.Should().Throw<ArgumentException>();

        slip.Cancel("wrong salary");
        slip.PaymentStatus.Should().Be((int)PaymentStatus.Cancelled);
        slip.DomainEvents.OfType<PaySlipVoidedEvent>().Single().Reason.Should().Be("wrong salary");
        var pay = () => slip.MarkPaid(DateTime.UtcNow);
        pay.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void No_other_payroll_event_name_contains_the_generated_substring_reporting_matches_on()
    {
        typeof(PaySlipPaidEvent).Name.Should().NotContain("PaySlipGenerated");
        typeof(PaySlipVoidedEvent).Name.Should().NotContain("PaySlipGenerated");
    }

    private static (Mock<IHrmUnitOfWork> Uow, PaySlip Slip) UowWith(PaySlip? slip)
    {
        var repo = new Mock<IPaySlipRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(slip);
        var uow = new Mock<IHrmUnitOfWork>();
        uow.SetupGet(u => u.PaySlips).Returns(repo.Object);
        return (uow, slip!);
    }

    [Fact]
    public async Task The_handlers_save_a_valid_transition_and_turn_a_refusal_into_a_business_rule_error()
    {
        var (uow, slip) = UowWith(Slip());

        var dto = await new PayPaySlipCommandHandler(uow.Object).Handle(new PayPaySlipCommand(slip.Id, null, "Cash"), default);

        dto.PaymentStatus.Should().Be("Paid");
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        var again = () => new CancelPaySlipCommandHandler(uow.Object).Handle(new CancelPaySlipCommand(slip.Id, "late"), default);
        await again.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task An_unknown_slip_is_not_found()
    {
        var (uow, _) = UowWith(null);

        var act = () => new PayPaySlipCommandHandler(uow.Object).Handle(new PayPaySlipCommand(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
