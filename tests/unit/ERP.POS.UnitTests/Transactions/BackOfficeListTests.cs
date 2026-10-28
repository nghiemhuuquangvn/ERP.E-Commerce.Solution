using ERP.POS.Application.Handlers;
using ERP.POS.Application.Interfaces;
using ERP.POS.Application.Queries;
using ERP.POS.Domain.Entities;
using ERP.POS.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.POS.UnitTests.Transactions;

/// <summary>ROADMAP P2-1a: back-office lists of transactions, shifts and one terminal.</summary>
public class BackOfficeListTests
{
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<ICashShiftRepository> _shifts = new();

    private static Transaction Sale()
    {
        var t = Transaction.Open(Guid.NewGuid(), Guid.NewGuid());
        t.AddItem(Guid.NewGuid(), "Ao thun", 2, 100m);
        t.Complete("Cash");
        return t;
    }

    [Fact]
    public async Task Transactions_are_searched_with_an_inclusive_last_day_and_a_clamped_page()
    {
        var terminal = Guid.NewGuid();
        _transactions.Setup(r => r.SearchAsync(terminal, null, TransactionStatus.Completed, new DateTime(2026, 10, 1), new DateTime(2026, 10, 29), 0, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Transaction> { Sale() }, 7));

        var result = await new ListTransactionsQueryHandler(_transactions.Object).Handle(new ListTransactionsQuery
        {
            TerminalId = terminal, Status = "completed", From = new DateTime(2026, 10, 1, 13, 0, 0), To = new DateTime(2026, 10, 28, 8, 0, 0), Page = 0, PageSize = 5000,
        }, default);

        result.Total.Should().Be(7);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(100);
        result.Items.Should().ContainSingle().Which.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task The_second_page_skips_the_first()
    {
        _transactions.Setup(r => r.SearchAsync(null, null, null, null, null, 20, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Transaction>(), 25));

        var result = await new ListTransactionsQueryHandler(_transactions.Object).Handle(new ListTransactionsQuery { Page = 2 }, default);

        result.Page.Should().Be(2);
        result.Total.Should().Be(25);
        _transactions.VerifyAll();
    }

    [Fact]
    public async Task An_unknown_status_is_a_business_rule_error_not_a_server_error()
    {
        var act = () => new ListShiftsQueryHandler(_shifts.Object).Handle(new ListShiftsQuery { Status = "Floating" }, default);

        await act.Should().ThrowAsync<BusinessRuleException>();
        _shifts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Shifts_are_filtered_by_terminal_and_status()
    {
        var terminal = Guid.NewGuid();
        var shift = CashShift.Open(terminal, Guid.NewGuid(), 500m, null);
        _shifts.Setup(r => r.SearchAsync(terminal, CashShiftStatus.Open, null, null, 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<CashShift> { shift }, 1));

        var result = await new ListShiftsQueryHandler(_shifts.Object).Handle(new ListShiftsQuery { TerminalId = terminal, Status = "Open" }, default);

        result.Items.Should().ContainSingle().Which.Status.Should().Be("Open");
    }

    [Fact]
    public async Task One_terminal_is_read_or_reported_missing()
    {
        var terminals = new Mock<ITerminalRepository>();
        var terminal = Terminal.Create("Front desk", null);
        terminals.Setup(r => r.GetByIdAsync(terminal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(terminal);
        var handler = new GetTerminalQueryHandler(terminals.Object);

        (await handler.Handle(new GetTerminalQuery { TerminalId = terminal.Id }, default))!.Name.Should().Be("Front desk");
        (await handler.Handle(new GetTerminalQuery { TerminalId = Guid.NewGuid() }, default)).Should().BeNull();
    }
}
