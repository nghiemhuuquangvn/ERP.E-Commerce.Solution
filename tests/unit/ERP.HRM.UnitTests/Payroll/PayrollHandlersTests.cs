using ERP.HRM.Application.Commands;
using ERP.HRM.Application.DTOs;
using ERP.HRM.Application.Handlers;
using ERP.HRM.Application.Interfaces;
using ERP.HRM.Domain.Entities;
using ERP.HRM.Domain.Enums;
using ERP.HRM.Domain.Events;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.HRM.UnitTests.Payroll;

/// <summary>ROADMAP P1-7: process payroll, leave balances and the settings rules, against mocked repositories.</summary>
public class PayrollHandlersTests
{
    private readonly Mock<IHrmUnitOfWork> _uow = new();
    private readonly Mock<IPayrollRepository> _payroll = new();
    private readonly Mock<IEmployeeSnapshotRepository> _employees = new();
    private readonly Mock<IPaySlipRepository> _slips = new();
    private readonly Mock<IAttendanceRepository> _attendance = new();
    private readonly Mock<ILeaveRequestRepository> _leave = new();
    private readonly List<PaySlip> _added = new();
    private readonly PayrollRateSet _rates = DefaultPayrollRates.Create();

    public PayrollHandlersTests()
    {
        _uow.SetupGet(u => u.Payroll).Returns(_payroll.Object);
        _uow.SetupGet(u => u.EmployeeSnapshots).Returns(_employees.Object);
        _uow.SetupGet(u => u.PaySlips).Returns(_slips.Object);
        _uow.SetupGet(u => u.Attendance).Returns(_attendance.Object);
        _uow.SetupGet(u => u.LeaveRequests).Returns(_leave.Object);
        _payroll.Setup(p => p.GetRateSetAsync(It.IsAny<DateTime>(), default)).ReturnsAsync(_rates);
        _payroll.Setup(p => p.AnyRateSetAsync(default)).ReturnsAsync(true);
        _payroll.Setup(p => p.ListDependentsAsync(It.IsAny<Guid>(), default)).ReturnsAsync(new List<Dependent>());
        _slips.Setup(s => s.Add(It.IsAny<PaySlip>())).Callback<PaySlip>(_added.Add);
        _attendance.Setup(a => a.GetMonthlyCountsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), default)).ReturnsAsync((0, 0, 0));
        _leave.Setup(l => l.GetApprovedOverlappingAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
              .ReturnsAsync(new List<LeaveRequest>());
    }

    private ProcessPayrollCommandHandler Process() =>
        new(_uow.Object, new PayrollSupport(_uow.Object), NullLogger<ProcessPayrollCommandHandler>.Instance);

    private EmployeeSnapshot Employee(string number = "E1") =>
        EmployeeSnapshot.Create(Guid.NewGuid(), "Employee " + number, (int)EmploymentStatus.Active, Guid.NewGuid(), number);

    private void WithTerms(EmployeeSnapshot e, decimal baseSalary = 30_000_000m) =>
        _payroll.Setup(p => p.GetCompensationAsync(e.Id, It.IsAny<DateTime>(), default)).ReturnsAsync(new EmployeeCompensation
        {
            EmployeeId = e.Id, EffectiveFrom = new DateTime(2026, 1, 1), BaseSalary = baseSalary, InsuranceEnabled = true
        });

    [Fact]
    public async Task An_employee_with_salary_terms_gets_a_calculated_slip_and_the_event_carries_the_split()
    {
        var e = Employee();
        _employees.Setup(r => r.GetActiveAsync(default)).ReturnsAsync(new List<EmployeeSnapshot> { e });
        WithTerms(e);

        var result = await Process().Handle(new ProcessPayrollCommand(2026, 10), default);

        result.Skipped.Should().BeEmpty();
        var slip = _added.Single();
        slip.GrossSalary.Should().Be(30_000_000m);
        slip.EmployeeSocial.Should().Be(2_400_000m);
        slip.PersonalIncomeTax.Should().BeGreaterThan(0m);
        slip.Deductions.Should().Be(slip.EmployeeSocial + slip.EmployeeHealth + slip.EmployeeUnemployment + slip.PersonalIncomeTax);
        slip.RateSetId.Should().Be(_rates.Id);
        var evt = slip.DomainEvents.OfType<PaySlipGeneratedEvent>().Single();
        evt.EmployerSocial.Should().Be(5_250_000m);
        evt.IncomeTax.Should().Be(slip.PersonalIncomeTax);
        evt.NetPay.Should().Be(evt.GrossSalary + evt.OvertimePay - evt.Deductions);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task An_employee_without_salary_terms_is_skipped_and_listed_not_given_invented_figures()
    {
        var withTerms = Employee("E1");
        var without = Employee("E2");
        _employees.Setup(r => r.GetActiveAsync(default)).ReturnsAsync(new List<EmployeeSnapshot> { withTerms, without });
        WithTerms(withTerms);
        _payroll.Setup(p => p.GetCompensationAsync(without.Id, It.IsAny<DateTime>(), default)).ReturnsAsync((EmployeeCompensation?)null);

        var result = await Process().Handle(new ProcessPayrollCommand(2026, 10), default);

        result.Created.Should().HaveCount(1);
        result.Skipped.Should().ContainSingle().Which.EmployeeNumber.Should().Be("E2");
    }

    [Fact]
    public async Task A_period_that_already_has_a_slip_is_left_alone()
    {
        var e = Employee();
        _employees.Setup(r => r.GetActiveAsync(default)).ReturnsAsync(new List<EmployeeSnapshot> { e });
        WithTerms(e);
        _slips.Setup(s => s.ExistsForPeriodAsync(e.Id, 2026, 10, default)).ReturnsAsync(true);

        var result = await Process().Handle(new ProcessPayrollCommand(2026, 10), default);

        result.Created.Should().BeEmpty();
        result.Skipped.Should().BeEmpty();
        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task Absent_days_half_days_and_unpaid_leave_reduce_the_paid_days()
    {
        var e = Employee();
        _employees.Setup(r => r.GetActiveAsync(default)).ReturnsAsync(new List<EmployeeSnapshot> { e });
        WithTerms(e, 22_000_000m);
        // October 2026 has 22 weekdays: 1 absent + 2 half days = 2 off, plus 3 unpaid leave days (Mon 5 - Wed 7).
        _attendance.Setup(a => a.GetMonthlyCountsAsync(e.Id, 2026, 10, default)).ReturnsAsync((20, 1, 2));
        _leave.Setup(l => l.GetApprovedOverlappingAsync(e.Id, (int)LeaveType.Unpaid, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
              .ReturnsAsync(new List<LeaveRequest>
              {
                  LeaveRequest.Create("LR", e.Id, (int)LeaveType.Unpaid, new DateTime(2026, 10, 5), new DateTime(2026, 10, 7), 3)
              });

        await Process().Handle(new ProcessPayrollCommand(2026, 10), default);

        var slip = _added.Single();
        slip.StandardDays.Should().Be(22m);
        slip.WorkedDays.Should().Be(17m);
        slip.GrossSalary.Should().Be(17_000_000m);
    }

    [Fact]
    public async Task A_month_without_any_attendance_record_pays_the_full_month()
    {
        var e = Employee();
        _employees.Setup(r => r.GetActiveAsync(default)).ReturnsAsync(new List<EmployeeSnapshot> { e });
        WithTerms(e, 22_000_000m);

        await Process().Handle(new ProcessPayrollCommand(2026, 10), default);

        _added.Single().WorkedDays.Should().Be(22m);
    }

    [Fact]
    public async Task Dependents_in_force_for_the_month_lower_the_tax()
    {
        var e = Employee();
        _employees.Setup(r => r.GetActiveAsync(default)).ReturnsAsync(new List<EmployeeSnapshot> { e });
        WithTerms(e);
        _payroll.Setup(p => p.ListDependentsAsync(e.Id, default)).ReturnsAsync(new List<Dependent>
        {
            new() { EmployeeId = e.Id, FullName = "Child", Relationship = "Child", FromMonth = new DateTime(2026, 1, 1) },
            new() { EmployeeId = e.Id, FullName = "Past", Relationship = "Parent", FromMonth = new DateTime(2025, 1, 1), ToMonth = new DateTime(2026, 6, 1) }
        });

        await Process().Handle(new ProcessPayrollCommand(2026, 10), default);

        _added.Single().DependentCount.Should().Be(1);
    }

    [Fact]
    public async Task A_tenant_with_no_rate_set_gets_the_default_draft_so_payroll_can_run()
    {
        _payroll.Setup(p => p.GetRateSetAsync(It.IsAny<DateTime>(), default)).ReturnsAsync((PayrollRateSet?)null);
        _payroll.Setup(p => p.AnyRateSetAsync(default)).ReturnsAsync(false);
        var e = Employee();
        _employees.Setup(r => r.GetActiveAsync(default)).ReturnsAsync(new List<EmployeeSnapshot> { e });
        WithTerms(e);

        await Process().Handle(new ProcessPayrollCommand(2026, 10), default);

        _payroll.Verify(p => p.Add(It.Is<PayrollRateSet>(s => !s.IsConfirmed)), Times.Once);
        _added.Should().HaveCount(1);
    }

    [Fact]
    public async Task A_month_before_every_rate_set_is_refused()
    {
        _payroll.Setup(p => p.GetRateSetAsync(It.IsAny<DateTime>(), default)).ReturnsAsync((PayrollRateSet?)null);
        _employees.Setup(r => r.GetActiveAsync(default)).ReturnsAsync(new List<EmployeeSnapshot>());

        var act = () => Process().Handle(new ProcessPayrollCommand(2020, 1), default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    // ─── annual leave ────────────────────────────────────────────────────

    private (SubmitLeaveRequestCommandHandler Submit, ApproveLeaveCommandHandler Approve, LeaveBalance Balance, Guid EmployeeId) Leave(decimal total)
    {
        var id = Guid.NewGuid();
        var balance = new LeaveBalance { EmployeeId = id, Year = 2026, LeaveType = (int)LeaveType.Annual, TotalDays = total };
        _payroll.Setup(p => p.GetLeaveBalanceAsync(id, 2026, (int)LeaveType.Annual, default)).ReturnsAsync(balance);
        _employees.Setup(r => r.GetByIdAsync(id, default)).ReturnsAsync((EmployeeSnapshot?)null);
        var support = new PayrollSupport(_uow.Object);
        return (new SubmitLeaveRequestCommandHandler(_uow.Object, NullLogger<SubmitLeaveRequestCommandHandler>.Instance, support),
                new ApproveLeaveCommandHandler(_uow.Object, NullLogger<ApproveLeaveCommandHandler>.Instance, support), balance, id);
    }

    [Fact]
    public async Task An_annual_leave_request_over_the_balance_is_refused()
    {
        var (submit, _, _, id) = Leave(total: 2m);

        // Mon 5 - Fri 9 October 2026 = 5 working days.
        var act = () => submit.Handle(new SubmitLeaveRequestCommand(id, (int)LeaveType.Annual, new DateTime(2026, 10, 5), new DateTime(2026, 10, 9), null), default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Message.Should().Contain("Not enough annual leave");
        _leave.Verify(l => l.Add(It.IsAny<LeaveRequest>()), Times.Never);
    }

    [Fact]
    public async Task Weekends_are_not_leave_days()
    {
        var (submit, _, _, id) = Leave(total: 12m);

        // Fri 9 - Mon 12 October = 2 working days.
        var dto = await submit.Handle(new SubmitLeaveRequestCommand(id, (int)LeaveType.Annual, new DateTime(2026, 10, 9), new DateTime(2026, 10, 12), null), default);

        dto.DaysRequested.Should().Be(2);
    }

    [Fact]
    public async Task A_request_on_a_weekend_only_is_refused()
    {
        var (submit, _, _, id) = Leave(total: 12m);

        var act = () => submit.Handle(new SubmitLeaveRequestCommand(id, (int)LeaveType.Annual, new DateTime(2026, 10, 10), new DateTime(2026, 10, 11), null), default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Approving_annual_leave_uses_up_the_balance_and_a_second_approval_beyond_it_is_refused()
    {
        var (_, approve, balance, id) = Leave(total: 5m);
        var first = LeaveRequest.Create("LR-1", id, (int)LeaveType.Annual, new DateTime(2026, 10, 5), new DateTime(2026, 10, 9), 5);
        var second = LeaveRequest.Create("LR-2", id, (int)LeaveType.Annual, new DateTime(2026, 11, 2), new DateTime(2026, 11, 3), 2);
        _leave.Setup(l => l.GetByIdAsync(first.Id, default)).ReturnsAsync(first);
        _leave.Setup(l => l.GetByIdAsync(second.Id, default)).ReturnsAsync(second);

        await approve.Handle(new ApproveLeaveCommand(first.Id), default);
        balance.UsedDays.Should().Be(5m);

        var act = () => approve.Handle(new ApproveLeaveCommand(second.Id), default);
        await act.Should().ThrowAsync<BusinessRuleException>();
        second.LeaveStatus.Should().Be((int)LeaveStatus.Pending);
    }

    [Fact]
    public async Task Sick_leave_does_not_touch_the_annual_balance()
    {
        var (_, approve, balance, id) = Leave(total: 1m);
        var sick = LeaveRequest.Create("LR-3", id, (int)LeaveType.Sick, new DateTime(2026, 10, 5), new DateTime(2026, 10, 9), 5);
        _leave.Setup(l => l.GetByIdAsync(sick.Id, default)).ReturnsAsync(sick);

        await approve.Handle(new ApproveLeaveCommand(sick.Id), default);

        balance.UsedDays.Should().Be(0m);
    }

    // ─── settings ────────────────────────────────────────────────────────

    private SaveRateSetCommand RateSet(List<PitBracketDto> brackets, Guid? id = null) => new(
        id, new DateTime(2027, 1, 15), "2027 rates", 0.08m, 0.015m, 0.01m, 0.175m, 0.03m, 0.01m,
        15_500_000m, 6_200_000m, 46_800_000m, 106_200_000m, 12m, true, brackets);

    [Fact]
    public async Task A_rate_set_is_saved_from_the_first_of_the_month_with_ordered_brackets()
    {
        _payroll.Setup(p => p.ListRateSetsAsync(default)).ReturnsAsync(new List<PayrollRateSet>());
        var handler = new SaveRateSetCommandHandler(_uow.Object);

        var dto = await handler.Handle(RateSet(new() { new(null, 0.35m), new(10_000_000m, 0.05m) }), default);

        dto.EffectiveFrom.Should().Be(new DateTime(2027, 1, 1));
        dto.Brackets.Select(b => b.UpTo).Should().Equal(10_000_000m, null);
        dto.IsConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task A_bracket_table_without_exactly_one_open_end_is_refused()
    {
        var handler = new SaveRateSetCommandHandler(_uow.Object);

        var noOpenEnd = () => handler.Handle(RateSet(new() { new(10_000_000m, 0.05m) }), default);
        var twoOpenEnds = () => handler.Handle(RateSet(new() { new(null, 0.05m), new(null, 0.1m) }), default);
        var rateOver100 = () => handler.Handle(RateSet(new() { new(10_000_000m, 1.5m), new(null, 0.1m) }), default);

        await noOpenEnd.Should().ThrowAsync<BusinessRuleException>();
        await twoOpenEnds.Should().ThrowAsync<BusinessRuleException>();
        await rateOver100.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Two_rate_sets_cannot_start_in_the_same_month()
    {
        _payroll.Setup(p => p.ListRateSetsAsync(default)).ReturnsAsync(new List<PayrollRateSet>
        {
            new() { EffectiveFrom = new DateTime(2027, 1, 1) }
        });
        var handler = new SaveRateSetCommandHandler(_uow.Object);

        var act = () => handler.Handle(RateSet(new() { new(null, 0.35m) }), default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Salary_terms_need_a_positive_base_salary_and_an_active_employee()
    {
        var e = Employee();
        _employees.Setup(r => r.GetByIdAsync(e.Id, default)).ReturnsAsync(e);
        var handler = new SaveCompensationCommandHandler(_uow.Object);

        var zero = () => handler.Handle(new SaveCompensationCommand(e.Id, new DateTime(2026, 10, 12), 0m, 0m, 0m, null, true, null, null), default);
        await zero.Should().ThrowAsync<BusinessRuleException>();

        var dto = await handler.Handle(new SaveCompensationCommand(e.Id, new DateTime(2026, 10, 12), 20_000_000m, 0m, 500_000m, null, true, " 0123456789 ", null), default);
        dto.EffectiveFrom.Should().Be(new DateTime(2026, 10, 1));
        dto.TaxCode.Should().Be("0123456789");
    }
}
