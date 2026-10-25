using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Vat;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Posting;

/// <summary>ROADMAP P1-3b: the settlement of a period's VAT (Dr VAT output / Cr VAT input for the smaller balance).</summary>
public class VatSettlementTests
{
    private readonly Account _input = Account.Create("1331", "Input VAT", 1, 1);
    private readonly Account _output = Account.Create("3331", "Output VAT", 2, 2);
    private readonly FiscalPeriod _period = FiscalPeriod.Create("October 2026", new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));
    private readonly Dictionary<string, JournalEntry> _entries = new();
    private readonly List<PostingRequest> _posted = new();
    private decimal _outputCredit;
    private decimal _inputDebit;
    private bool _shared;

    private (SettleVatCommandHandler Settle, GetVatSettlementQueryHandler Query) Handlers()
    {
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _shared ? new List<Account> { _output } : new List<Account> { _input, _output });
        var mappings = new Mock<IPostingAccountMappingRepository>();
        mappings.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() =>
            _shared
                ? new List<PostingAccountMapping> { PostingAccountMapping.Create(Guid.NewGuid(), PostingKeys.VatOutput, _output.Id), PostingAccountMapping.Create(Guid.NewGuid(), PostingKeys.VatInput, _output.Id) }
                : new List<PostingAccountMapping>());
        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(l => l.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new List<AccountBalanceSummary>
            {
                new() { AccountId = _output.Id, TotalCredit = _outputCredit, TotalDebit = 0m },
                new() { AccountId = _input.Id, TotalDebit = _inputDebit, TotalCredit = 0m },
            });
        var periods = new Mock<IFiscalPeriodRepository>();
        periods.Setup(p => p.GetByIdAsync(_period.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_period);
        var journal = new Mock<IJournalEntryRepository>();
        journal.Setup(j => j.GetBySourceKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string key, CancellationToken _) => _entries.GetValueOrDefault(key));
        var uow = new Mock<IAccountingUnitOfWork>();
        uow.SetupGet(u => u.Accounts).Returns(accounts.Object);
        uow.SetupGet(u => u.PostingMappings).Returns(mappings.Object);
        uow.SetupGet(u => u.JournalLines).Returns(lines.Object);
        uow.SetupGet(u => u.FiscalPeriods).Returns(periods.Object);
        uow.SetupGet(u => u.JournalEntries).Returns(journal.Object);
        var posting = new Mock<IJournalPostingService>();
        posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((PostingRequest r, CancellationToken _) =>
        {
            _posted.Add(r);
            var entry = JournalEntry.Create($"JE-{_posted.Count}", _period.Id, r.Description);
            entry.Status = (int)JournalStatus.Posted;
            _entries[JournalEntry.BuildSourceKey(r.SourceType, r.SourceId)] = entry;
            return entry;
        });
        return (new SettleVatCommandHandler(uow.Object, posting.Object, NullLogger<SettleVatCommandHandler>.Instance), new GetVatSettlementQueryHandler(uow.Object));
    }

    private SettleVatCommand Cmd() => new() { FiscalPeriodId = _period.Id };

    [Fact]
    public async Task More_output_than_input_settles_the_input_and_leaves_the_rest_payable()
    {
        (_outputCredit, _inputDebit) = (200_000m, 150_000m);

        var dto = await Handlers().Settle.Handle(Cmd(), default);

        (dto.Amount, dto.PayableAfter, dto.CarryForwardAfter, dto.Settled).Should().Be((150_000m, 50_000m, 0m, true));
        var request = _posted.Single();
        request.Lines.Single(l => l.Debit > 0).PostingKey.Should().Be(PostingKeys.VatOutput);
        request.Lines.Single(l => l.Credit > 0).PostingKey.Should().Be(PostingKeys.VatInput);
        request.Lines.Sum(l => l.Debit).Should().Be(150_000m);
        request.Date.Should().Be(new DateTime(2026, 10, 31));
    }

    [Fact]
    public async Task More_input_than_output_settles_the_output_and_carries_the_rest_forward()
    {
        (_outputCredit, _inputDebit) = (100_000m, 230_000m);

        var dto = await Handlers().Settle.Handle(Cmd(), default);

        (dto.Amount, dto.PayableAfter, dto.CarryForwardAfter).Should().Be((100_000m, 0m, 130_000m));
    }

    [Fact]
    public async Task A_settled_period_is_refused_until_the_entry_is_reversed_and_then_settles_again_under_a_new_round()
    {
        (_outputCredit, _inputDebit) = (200_000m, 150_000m);
        var handlers = Handlers();
        await handlers.Settle.Handle(Cmd(), default);

        var again = () => handlers.Settle.Handle(Cmd(), default);
        await again.Should().ThrowAsync<BusinessRuleException>().WithMessage("*already settled*");
        (await handlers.Query.Handle(new GetVatSettlementQuery { FiscalPeriodId = _period.Id }, default)).Should().Match<VatSettlementDto>(d => d.Settled && d.EntryNumber == "JE-1");

        _entries.Values.Single().Status = (int)JournalStatus.Reversed;
        (_outputCredit, _inputDebit) = (260_000m, 150_000m);
        var second = await handlers.Settle.Handle(Cmd(), default);

        second.Amount.Should().Be(150_000m);
        _posted.Should().HaveCount(2);
        _posted[1].SourceId.Should().NotBe(_posted[0].SourceId);
    }

    [Fact]
    public async Task Nothing_is_posted_when_one_side_is_zero()
    {
        (_outputCredit, _inputDebit) = (200_000m, 0m);

        var dto = await Handlers().Settle.Handle(Cmd(), default);

        dto.Amount.Should().Be(0m);
        dto.Settled.Should().BeFalse();
        _posted.Should().BeEmpty();
        dto.Notes.Should().Contain(n => n.Contains("Nothing to settle"));
    }

    [Fact]
    public async Task One_shared_account_has_nothing_to_settle()
    {
        _shared = true;
        (_outputCredit, _inputDebit) = (200_000m, 150_000m);

        var dto = await Handlers().Settle.Handle(Cmd(), default);

        dto.SharedAccount.Should().BeTrue();
        _posted.Should().BeEmpty();
    }

    [Fact]
    public async Task A_closed_period_is_refused()
    {
        (_outputCredit, _inputDebit) = (200_000m, 150_000m);
        _period.Close();

        var act = () => Handlers().Settle.Handle(Cmd(), default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*closed*");
    }

    [Fact]
    public async Task The_figures_can_be_read_without_posting()
    {
        (_outputCredit, _inputDebit) = (200_000m, 150_000m);

        var dto = await Handlers().Query.Handle(new GetVatSettlementQuery { FiscalPeriodId = _period.Id }, default);

        (dto.OutputVat, dto.InputVat, dto.Amount, dto.Settled).Should().Be((200_000m, 150_000m, 150_000m, false));
        _posted.Should().BeEmpty();
    }
}
