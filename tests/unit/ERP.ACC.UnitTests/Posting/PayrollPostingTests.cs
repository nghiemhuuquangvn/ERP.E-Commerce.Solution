using ERP.ACC.Application.EventHandlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.BackgroundJobs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ERP.ACC.UnitTests.Posting;

/// <summary>ROADMAP P1-1d: pay slips, their payment and cancellation reach the ledger.</summary>
public class PayrollPostingTests
{
    private static readonly DateTime Today = new(2026, 10, 3);

    private static Dictionary<string, (decimal Dr, decimal Cr)> ByKey(PostingRequest r) =>
        r.Lines.ToDictionary(l => l.PostingKey!, l => (l.Debit, l.Credit));

    [Fact]
    public void A_pay_slip_books_gross_and_overtime_as_expense_against_net_pay_and_withholdings()
    {
        var request = PostingRules.ForPaySlip(Guid.NewGuid(), "2026-09", 5000m, 100m, 1500m, new DateTime(2026, 9, 30), Today)!;

        request.SourceType.Should().Be("PaySlip");
        var lines = ByKey(request);
        lines[PostingKeys.SalaryExpense].Should().Be((5100m, 0m));
        lines[PostingKeys.SalariesPayable].Should().Be((0m, 3600m));
        lines[PostingKeys.PayrollWithholdingsPayable].Should().Be((0m, 1500m));
        request.Lines.Sum(l => l.Debit).Should().Be(request.Lines.Sum(l => l.Credit));
    }

    // ROADMAP P1-7: base 30,000,000 + 1,000,000 allowance; employee insurance 3,150,000, income tax 635,000, employer insurance 6,450,000.
    private static readonly PaySlipBreakdown Split = new(2_400_000m, 450_000m, 300_000m, 5_250_000m, 900_000m, 300_000m, 635_000m);

    [Fact]
    public void A_pay_slip_with_a_breakdown_splits_the_withholdings_and_books_the_employer_cost()
    {
        var request = PostingRules.ForPaySlip(Guid.NewGuid(), "2026-10", 31_000_000m, 0m, 3_785_000m, new DateTime(2026, 10, 31), new DateTime(2026, 11, 2), Split)!;

        var lines = ByKey(request);
        lines[PostingKeys.SalaryExpense].Should().Be((31_000_000m, 0m));
        lines[PostingKeys.EmployerInsuranceExpense].Should().Be((6_450_000m, 0m));
        lines[PostingKeys.SalariesPayable].Should().Be((0m, 27_215_000m));
        lines[PostingKeys.SocialInsurancePayable].Should().Be((0m, 7_650_000m));      // 2,400,000 + 5,250,000
        lines[PostingKeys.HealthInsurancePayable].Should().Be((0m, 1_350_000m));      // 450,000 + 900,000
        lines[PostingKeys.UnemploymentInsurancePayable].Should().Be((0m, 600_000m));  // 300,000 + 300,000
        lines[PostingKeys.PitPayable].Should().Be((0m, 635_000m));
        request.Lines.Should().NotContain(l => l.PostingKey == PostingKeys.PayrollWithholdingsPayable);
        request.Lines.Sum(l => l.Debit).Should().Be(request.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public void A_withholding_the_breakdown_does_not_explain_stays_on_the_lump_account()
    {
        // 3,785,000 explained, 15,000 of another deduction.
        var request = PostingRules.ForPaySlip(Guid.NewGuid(), "2026-10", 31_000_000m, 0m, 3_800_000m, null, Today, Split)!;

        ByKey(request)[PostingKeys.PayrollWithholdingsPayable].Should().Be((0m, 15_000m));
        request.Lines.Sum(l => l.Debit).Should().Be(request.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public void A_breakdown_larger_than_the_amount_withheld_books_nothing()
    {
        PostingRules.ForPaySlip(Guid.NewGuid(), "2026-10", 31_000_000m, 0m, 1_000_000m, null, Today, Split).Should().BeNull();
    }

    [Fact]
    public void An_empty_breakdown_posts_the_legacy_entry()
    {
        var request = PostingRules.ForPaySlip(Guid.NewGuid(), "2026-09", 5000m, 100m, 1500m, null, Today, new PaySlipBreakdown(0, 0, 0, 0, 0, 0, 0))!;

        ByKey(request).Keys.Should().BeEquivalentTo(new[] { PostingKeys.SalaryExpense, PostingKeys.SalariesPayable, PostingKeys.PayrollWithholdingsPayable });
    }

    [Fact]
    public void The_consumer_reads_the_insurance_and_tax_split_of_the_event()
    {
        var parsed = AccountingKafkaConsumer.ParsePaySlipGenerated(JObject.Parse(
            "{\"PaySlipId\":\"" + Guid.NewGuid() + "\",\"PayPeriod\":\"2026-10\",\"GrossSalary\":31000000,\"Deductions\":3785000,\"NetPay\":27215000," +
            "\"EmployeeSocial\":2400000,\"EmployeeHealth\":450000,\"EmployeeUnemployment\":300000,\"EmployerSocial\":5250000,\"EmployerHealth\":900000,\"EmployerUnemployment\":300000,\"IncomeTax\":635000}"));

        parsed.Breakdown.Should().Be(Split);
        parsed.Breakdown.HasValues.Should().BeTrue();
    }

    [Fact]
    public void The_entry_is_dated_the_end_of_the_pay_period_but_never_in_the_future()
    {
        PostingRules.ForPaySlip(Guid.NewGuid(), "2026-09", 100m, 0m, 0m, new DateTime(2026, 9, 30), Today)!
            .Date.Should().Be(new DateTime(2026, 9, 30));
        PostingRules.ForPaySlip(Guid.NewGuid(), "2026-10", 100m, 0m, 0m, new DateTime(2026, 10, 31), Today)!
            .Date.Should().Be(Today.Date);
        PostingRules.ForPaySlip(Guid.NewGuid(), "2026-10", 100m, 0m, 0m, null, Today)!
            .Date.Should().Be(Today.Date);
    }

    [Fact]
    public void A_slip_without_deductions_has_no_withholding_line_and_deductions_above_pay_book_nothing()
    {
        PostingRules.ForPaySlip(Guid.NewGuid(), "2026-09", 100m, 0m, 0m, null, Today)!
            .Lines.Should().NotContain(l => l.PostingKey == PostingKeys.PayrollWithholdingsPayable);
        PostingRules.ForPaySlip(Guid.NewGuid(), "2026-09", 100m, 0m, 150m, null, Today).Should().BeNull();
    }

    [Fact]
    public void Paying_a_slip_clears_the_salaries_payable_against_the_bank()
    {
        var request = PostingRules.ForPaySlipPayment(Guid.NewGuid(), "2026-09", 3600m, Today)!;

        request.SourceType.Should().Be("PaySlipPayment");
        var lines = ByKey(request);
        lines[PostingKeys.SalariesPayable].Should().Be((3600m, 0m));
        lines[PostingKeys.Bank].Should().Be((0m, 3600m));
        PostingRules.ForPaySlipPayment(Guid.NewGuid(), "2026-09", 0m, Today).Should().BeNull();
    }

    private readonly Mock<IAccountingUnitOfWork> _uow = new();
    private readonly Mock<IJournalPostingService> _posting = new();
    private readonly Mock<IJournalEntryRepository> _entries = new();
    private readonly List<PostingRequest> _posted = new();

    public PayrollPostingTests()
    {
        _uow.SetupGet(u => u.JournalEntries).Returns(_entries.Object);
        _posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PostingRequest, CancellationToken>((r, _) => _posted.Add(r)).ReturnsAsync((JournalEntry?)null);
    }

    [Fact]
    public async Task A_generated_slip_is_posted_once_and_a_legacy_slip_without_amounts_is_skipped()
    {
        var handler = new PaySlipGeneratedIntegrationEventHandler(_uow.Object, _posting.Object, NullLogger<PaySlipGeneratedIntegrationEventHandler>.Instance);

        await handler.Handle(new PaySlipGeneratedIntegrationEvent
        {
            PaySlipId = Guid.NewGuid(), PayPeriod = "2026-09", GrossSalary = 5000m, Deductions = 1500m, NetPay = 3500m
        }, default);
        await handler.Handle(new PaySlipGeneratedIntegrationEvent { PaySlipId = Guid.NewGuid(), PayPeriod = "2026-08", NetPay = 3500m }, default);

        _posted.Should().ContainSingle();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_payment_is_booked_only_when_the_salary_entry_exists()
    {
        var paid = new PaySlipPaidIntegrationEvent { PaySlipId = Guid.NewGuid(), PayPeriod = "2026-09", NetPay = 3500m, PaidOn = Today };
        var handler = new PaySlipPaidIntegrationEventHandler(_uow.Object, _posting.Object, NullLogger<PaySlipPaidIntegrationEventHandler>.Instance);

        await handler.Handle(paid, default); // no accrual -> skipped
        _posted.Should().BeEmpty();

        var accrual = JournalEntry.CreatePosted("JE-1", Guid.NewGuid(), Today, "Payroll", "PaySlip", paid.PaySlipId,
            new[]
            {
                new JournalLineDraft(Guid.NewGuid(), 100m, 0m, ""), new JournalLineDraft(Guid.NewGuid(), 0m, 100m, ""),
            });
        _entries.Setup(e => e.GetBySourceKeyAsync(JournalEntry.BuildSourceKey("PaySlip", paid.PaySlipId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(accrual);

        await handler.Handle(paid, default);

        _posted.Should().ContainSingle().Which.SourceType.Should().Be("PaySlipPayment");
    }

    [Fact]
    public async Task A_cancelled_slip_reverses_its_entry()
    {
        var reversed = new List<(string Type, Guid Id)>();
        _posting.Setup(p => p.ReverseAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid, string, DateTime, CancellationToken>((t, id, _, _, _) => reversed.Add((t, id))).ReturnsAsync((JournalEntry?)null);
        var id = Guid.NewGuid();

        await new PaySlipVoidedIntegrationEventHandler(_uow.Object, _posting.Object, NullLogger<PaySlipVoidedIntegrationEventHandler>.Instance)
            .Handle(new PaySlipVoidedIntegrationEvent { PaySlipId = id, Reason = "wrong salary" }, default);

        reversed.Should().Equal(("PaySlip", id));
    }

    [Fact]
    public void The_consumer_reads_the_newtonsoft_payloads_hrm_publishes()
    {
        var id = Guid.NewGuid();
        var generated = AccountingKafkaConsumer.ParsePaySlipGenerated(JObject.Parse(
            "{\"$type\":\"ERP.HRM.Domain.Events.PaySlipGeneratedEvent, ERP.HRM.Domain\",\"PaySlipId\":\"" + id +
            "\",\"NetPay\":3600.0,\"PayPeriod\":\"2026-09\",\"GrossSalary\":5000.0,\"OvertimePay\":100.0,\"Deductions\":1500.0,\"PeriodEnd\":\"2026-09-30T00:00:00\"}"));
        generated.PaySlipId.Should().Be(id);
        generated.GrossSalary.Should().Be(5000m);
        generated.OvertimePay.Should().Be(100m);
        generated.Deductions.Should().Be(1500m);
        generated.PeriodEnd.Should().Be(new DateTime(2026, 9, 30));

        var legacy = AccountingKafkaConsumer.ParsePaySlipGenerated(JObject.Parse(
            "{\"PaySlipId\":\"" + id + "\",\"NetPay\":3500.0,\"PayPeriod\":\"2026-08\"}"));
        (legacy.GrossSalary + legacy.OvertimePay).Should().Be(0m);

        var paid = AccountingKafkaConsumer.ParsePaySlipPaid(JObject.Parse(
            "{\"PaySlipId\":\"" + id + "\",\"NetPay\":3600.0,\"PayPeriod\":\"2026-09\",\"PaidOn\":\"2026-10-05T00:00:00Z\"}"));
        paid.NetPay.Should().Be(3600m);
        paid.PaidOn.Date.Should().Be(new DateTime(2026, 10, 5));

        AccountingKafkaConsumer.ParsePaySlipVoided(JObject.Parse("{\"PaySlipId\":\"" + id + "\",\"Reason\":\"x\"}")).Reason.Should().Be("x");
    }
}
