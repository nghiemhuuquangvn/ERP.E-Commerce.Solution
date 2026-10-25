using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.BackgroundJobs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Posting;

/// <summary>ROADMAP P1-2l: the month-end accrual job — its date, the opt-in switch and the closed-period guard of the command it sends.</summary>
public class LoanAccrualJobTests
{
    [Theory]
    [InlineData("2026-11-01", "2026-10-31")]
    [InlineData("2026-11-25", "2026-10-31")]
    [InlineData("2027-01-01", "2026-12-31")]
    [InlineData("2028-03-10", "2028-02-29")]
    public void The_job_accrues_to_the_last_day_of_the_previous_month(string today, string expected)
    {
        LoanInterestAccrualJob.AccrualDate(DateTime.Parse(today)).Should().Be(DateTime.Parse(expected));
    }

    [Fact]
    public void The_automatic_accrual_is_off_until_the_tenant_switches_it_on()
    {
        var policy = ClosingPolicy.Create(Guid.NewGuid(), ERP.ACC.Domain.Enums.ClosingMode.Monthly, 12);

        policy.AutoAccrueLoanInterest.Should().BeFalse();
        policy.SetAutoAccrueLoanInterest(true);
        policy.AutoAccrueLoanInterest.Should().BeTrue();
    }

    private static (Mock<IAccountingUnitOfWork> Uow, Mock<IJournalPostingService> Posting, Loan Loan) Setup(FiscalPeriod? period)
    {
        var loan = Loan.Create("LN-J", "ACB", 2000m, 9m, new DateTime(2026, 9, 1),
            new[] { (new DateTime(2026, 12, 1), 1000m, 90m), (new DateTime(2028, 6, 1), 1000m, 400m) }, null, null);
        loan.MarkPosted();
        var loans = new Mock<ILoanRepository>();
        loans.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Loan> { loan });
        loans.Setup(r => r.GetByIdAsync(loan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(loan);
        var periods = new Mock<IFiscalPeriodRepository>();
        periods.Setup(p => p.GetContainingAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(period);
        var uow = new Mock<IAccountingUnitOfWork>();
        uow.SetupGet(u => u.Loans).Returns(loans.Object);
        uow.SetupGet(u => u.FiscalPeriods).Returns(periods.Object);
        var posting = new Mock<IJournalPostingService>();
        posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => JournalEntry.Create("JE-1", Guid.NewGuid(), "x"));
        return (uow, posting, loan);
    }

    [Fact]
    public async Task A_closed_period_is_skipped_when_the_job_asks_and_nothing_is_posted_or_remembered()
    {
        var october = FiscalPeriod.Create("October 2026", new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));
        october.Close();
        var (uow, posting, loan) = Setup(october);
        var handler = new AccrueLoanInterestCommandHandler(uow.Object, posting.Object, NullLogger<AccrueLoanInterestCommandHandler>.Instance);

        var result = await handler.Handle(new AccrueLoanInterestCommand { AsOfDate = new DateTime(2026, 10, 31), SkipClosedPeriod = true }, default);

        result.SkippedClosedPeriod.Should().BeTrue();
        result.Lines.Should().BeEmpty();
        posting.Verify(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        loan.Installments.Sum(i => i.AccruedInterest).Should().Be(0m);
    }

    [Fact]
    public async Task An_open_or_not_yet_created_period_is_accrued_and_a_user_run_is_never_skipped()
    {
        var october = FiscalPeriod.Create("October 2026", new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));
        var open = Setup(october);
        var handler = new AccrueLoanInterestCommandHandler(open.Uow.Object, open.Posting.Object, NullLogger<AccrueLoanInterestCommandHandler>.Instance);
        var job = new AccrueLoanInterestCommand { AsOfDate = new DateTime(2026, 10, 31), SkipClosedPeriod = true };

        (await handler.Handle(job, default)).TotalAccrued.Should().BeGreaterThan(0m);
        (await handler.Handle(job, default)).Lines.Should().BeEmpty();             // second tick: nothing more

        var missing = Setup(null);
        var handlerNoPeriod = new AccrueLoanInterestCommandHandler(missing.Uow.Object, missing.Posting.Object, NullLogger<AccrueLoanInterestCommandHandler>.Instance);
        (await handlerNoPeriod.Handle(job, default)).TotalAccrued.Should().BeGreaterThan(0m);

        var closed = FiscalPeriod.Create("October 2026", new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));
        closed.Close();
        var user = Setup(closed);
        var userHandler = new AccrueLoanInterestCommandHandler(user.Uow.Object, user.Posting.Object, NullLogger<AccrueLoanInterestCommandHandler>.Instance);
        (await userHandler.Handle(new AccrueLoanInterestCommand { AsOfDate = new DateTime(2026, 10, 31) }, default)).SkippedClosedPeriod.Should().BeFalse();
    }
}
