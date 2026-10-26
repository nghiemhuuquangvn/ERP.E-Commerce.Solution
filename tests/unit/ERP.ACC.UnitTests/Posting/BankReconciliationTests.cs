using ERP.ACC.Application.Bank;
using ERP.ACC.Application.Commands;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Posting;

/// <summary>ROADMAP P1-4: the CSV of a bank, the matching of its lines with the ledger, and the reconciliation that follows.</summary>
public class BankReconciliationTests
{
    // ---- CSV ----

    [Fact]
    public void A_csv_with_one_signed_amount_column_is_read()
    {
        var result = BankCsvParser.Parse("Date,Description,Reference,Amount,Balance\n2026-10-04,Customer ABC,INV-1,\"5,000,000\",5000000\n10/10/2026,Supplier XYZ,BILL-7,-2000000,3000000");

        result.Errors.Should().BeEmpty();
        result.Rows.Select(r => (r.Date, r.Amount, r.Reference, r.Balance)).Should().Equal(
            (new DateTime(2026, 10, 4), 5_000_000m, "INV-1", 5_000_000m),
            (new DateTime(2026, 10, 10), -2_000_000m, "BILL-7", 3_000_000m));
    }

    [Fact]
    public void Deposit_and_withdrawal_columns_in_Vietnamese_with_semicolons_are_read()
    {
        var result = BankCsvParser.Parse("Ngày GD;Nội dung;Số tham chiếu;Ghi nợ;Ghi có\n15/10/2026;Phí quản lý tài khoản;FEE1;50.000;\n16/10/2026;Lãi tiền gửi;INT1;;1.250,50");

        result.Errors.Should().BeEmpty();
        result.Rows.Select(r => (r.Description, r.Amount)).Should().Equal(("Phí quản lý tài khoản", -50_000m), ("Lãi tiền gửi", 1_250.50m));
    }

    [Theory]
    [InlineData("1,500,000", 1_500_000)]
    [InlineData("1.500.000", 1_500_000)]
    [InlineData("1 500 000", 1_500_000)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1234.5", 1234.5)]
    [InlineData("(500)", -500)]
    [InlineData("-500", -500)]
    public void Amounts_in_the_usual_notations_are_read(string text, double expected)
    {
        BankCsvParser.TryAmount(text, out var value).Should().BeTrue();
        value.Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12-34")]
    public void Not_an_amount_is_refused(string text) => BankCsvParser.TryAmount(text, out _).Should().BeFalse();

    [Fact]
    public void Bad_rows_and_a_bad_header_are_reported_with_their_line()
    {
        var noAmount = BankCsvParser.Parse("Date,Description\n2026-10-04,x");
        noAmount.Errors.Should().ContainSingle().Which.Message.Should().Contain("amount");

        var bad = BankCsvParser.Parse("Date,Amount\n2026-10-04,100\nnot a date,100\n2026-10-06,abc\n2026-10-07,0");

        bad.Rows.Should().ContainSingle();
        bad.Errors.Select(e => e.RowNumber).Should().Equal(3, 4, 5);
        BankCsvParser.Parse("").Errors.Should().ContainSingle();
    }

    // ---- matcher ----

    private static LedgerRow Row(string entry, DateTime date, decimal debit, decimal credit, string description = "") =>
        new() { LineId = Guid.NewGuid(), EntryId = Guid.NewGuid(), EntryNumber = entry, EntryDate = date, EntryDescription = description, LineDescription = "", Debit = debit, Credit = credit };

    private static BankStatementLine Line(DateTime date, decimal amount, string? reference = null) =>
        BankStatementLine.Create(Guid.NewGuid(), Guid.NewGuid(), date, "x", reference, amount);

    [Fact]
    public void A_line_matches_the_ledger_line_of_the_same_amount_within_the_tolerance()
    {
        var deposit = Row("JE-1", new DateTime(2026, 10, 3), 5_000_000m, 0m);
        var payment = Row("JE-2", new DateTime(2026, 10, 10), 0m, 2_000_000m);
        var lines = new[] { Line(new DateTime(2026, 10, 4), 5_000_000m), Line(new DateTime(2026, 10, 10), -2_000_000m), Line(new DateTime(2026, 10, 15), -50_000m) };

        var matches = BankMatcher.Match(lines, new[] { deposit, payment }, 3);

        matches.Select(m => m.JournalLineId).Should().BeEquivalentTo(new[] { deposit.LineId, payment.LineId });
        matches.Should().HaveCount(2);                                       // the fee has no entry
    }

    [Fact]
    public void A_date_beyond_the_tolerance_is_not_matched()
    {
        var matches = BankMatcher.Match(new[] { Line(new DateTime(2026, 10, 20), 100m) }, new[] { Row("JE-1", new DateTime(2026, 10, 10), 100m, 0m) }, 3);

        matches.Should().BeEmpty();
    }

    [Fact]
    public void Two_equal_candidates_are_left_for_a_person_unless_the_reference_or_the_date_decides()
    {
        var a = Row("JE-1", new DateTime(2026, 10, 4), 100m, 0m);
        var b = Row("JE-2", new DateTime(2026, 10, 4), 100m, 0m);

        BankMatcher.Match(new[] { Line(new DateTime(2026, 10, 4), 100m) }, new[] { a, b }).Should().BeEmpty();

        var byReference = BankMatcher.Match(new[] { Line(new DateTime(2026, 10, 4), 100m, "JE-2") }, new[] { a, b });
        byReference.Should().ContainSingle().Which.JournalLineId.Should().Be(b.LineId);

        var near = Row("JE-3", new DateTime(2026, 10, 5), 100m, 0m);
        var far = Row("JE-4", new DateTime(2026, 10, 7), 100m, 0m);
        BankMatcher.Match(new[] { Line(new DateTime(2026, 10, 5), 100m) }, new[] { far, near }).Should().ContainSingle().Which.JournalLineId.Should().Be(near.LineId);
    }

    [Fact]
    public void A_ledger_line_is_matched_once()
    {
        var only = Row("JE-1", new DateTime(2026, 10, 4), 100m, 0m);

        var matches = BankMatcher.Match(new[] { Line(new DateTime(2026, 10, 4), 100m, "JE-1"), Line(new DateTime(2026, 10, 4), 100m) }, new[] { only });

        matches.Should().ContainSingle();
    }

    [Fact]
    public void A_matched_line_is_not_matched_again()
    {
        var ledger = Row("JE-1", new DateTime(2026, 10, 4), 100m, 0m);
        var done = Line(new DateTime(2026, 10, 4), 100m);
        done.MatchTo(Guid.NewGuid());

        BankMatcher.Match(new[] { done }, new[] { ledger }).Should().BeEmpty();
    }

    // ---- handlers ----

    private class Books
    {
        public readonly Guid GlAccountId = Guid.NewGuid();
        public readonly BankAccount Account;
        public BankStatement Statement;
        public readonly List<BankStatementLine> Lines = new();
        public readonly List<LedgerRow> Ledger = new();
        public readonly Mock<IAccountingUnitOfWork> Uow = new();

        public Books(decimal opening, decimal closing, bool withGl = true)
        {
            Account = BankAccount.Create("Main", "VCB", "0011", 0m, "VND", withGl ? GlAccountId : null);
            Statement = BankStatement.Create(Account.Id, new DateTime(2026, 10, 31), opening, closing);

            var statements = new Mock<IBankStatementRepository>();
            statements.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Statement);
            var accounts = new Mock<IBankAccountRepository>();
            accounts.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Account);
            var glAccounts = new Mock<IAccountRepository>();
            glAccounts.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => null);
            var lineRepo = new Mock<IBankStatementLineRepository>();
            lineRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) => Lines.FirstOrDefault(l => l.Id == id));
            lineRepo.Setup(r => r.ListByStatementAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Lines.ToList());
            lineRepo.Setup(r => r.ListByBankAccountAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid _, DateTime to, CancellationToken __) => Lines.Where(l => l.LineDate <= to).ToList());
            lineRepo.Setup(r => r.MatchedJournalLineIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Lines.Where(l => l.MatchedJournalLineId.HasValue).Select(l => l.MatchedJournalLineId!.Value).ToHashSet());
            lineRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<BankStatementLine>>(), It.IsAny<CancellationToken>())).Callback<IEnumerable<BankStatementLine>, CancellationToken>((ls, _) => Lines.AddRange(ls)).Returns(Task.CompletedTask);
            lineRepo.Setup(r => r.Remove(It.IsAny<BankStatementLine>())).Callback<BankStatementLine>(l => Lines.Remove(l));
            var journalLines = new Mock<IJournalLineRepository>();
            journalLines.Setup(r => r.GetLedgerLinesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<Guid> _, DateTime? __, DateTime? to, CancellationToken ___) => Ledger.Where(r => !to.HasValue || r.EntryDate <= to).ToList());
            Uow.SetupGet(u => u.BankStatements).Returns(statements.Object);
            Uow.SetupGet(u => u.BankAccounts).Returns(accounts.Object);
            Uow.SetupGet(u => u.Accounts).Returns(glAccounts.Object);
            Uow.SetupGet(u => u.BankStatementLines).Returns(lineRepo.Object);
            Uow.SetupGet(u => u.JournalLines).Returns(journalLines.Object);
        }

        public LedgerRow Ledgered(string entry, DateTime date, decimal debit, decimal credit, string description = "")
        {
            var row = Row(entry, date, debit, credit, description);
            Ledger.Add(row);
            return row;
        }

        public BankStatementLine Statemented(DateTime date, decimal amount, string? reference = null)
        {
            var line = BankStatementLine.Create(Statement.Id, Account.Id, date, "x", reference, amount);
            Lines.Add(line);
            return line;
        }
    }

    private static readonly DateTime Oct4 = new(2026, 10, 4);
    private static readonly DateTime Oct10 = new(2026, 10, 10);
    private static readonly DateTime Oct15 = new(2026, 10, 15);

    [Fact]
    public async Task Everything_matched_and_explained_can_be_reconciled()
    {
        var b = new Books(0m, 3_000_000m);
        b.Ledgered("JE-1", Oct4, 5_000_000m, 0m);
        b.Ledgered("JE-2", Oct10, 0m, 2_000_000m);
        b.Statemented(Oct4, 5_000_000m);
        b.Statemented(Oct10, -2_000_000m);

        var dto = await new AutoMatchBankStatementCommandHandler(b.Uow.Object).Handle(new AutoMatchBankStatementCommand { BankStatementId = b.Statement.Id }, default);

        dto.Lines.Select(l => l.MatchStatus).Should().OnlyContain(s => s == "Matched");
        (dto.LedgerBalance, dto.OutstandingLedgerNet, dto.UnmatchedStatementNet, dto.Difference, dto.CanReconcile).Should().Be((3_000_000m, 0m, 0m, 0m, true));
        dto.UnmatchedLedger.Should().BeEmpty();

        var reconciled = await new ReconcileBankStatementCommandHandler(b.Uow.Object).Handle(new ReconcileBankStatementCommand { BankStatementId = b.Statement.Id }, default);
        reconciled.Status.Should().Be("Reconciled");
    }

    [Fact]
    public async Task A_cheque_not_yet_cashed_is_outstanding_and_explains_the_difference()
    {
        var b = new Books(0m, 3_000_000m);
        b.Ledgered("JE-1", Oct4, 5_000_000m, 0m);
        b.Ledgered("JE-2", Oct10, 0m, 2_000_000m);
        b.Ledgered("JE-3", new DateTime(2026, 10, 30), 0m, 1_000_000m, "cheque 77");        // not on the statement
        b.Statemented(Oct4, 5_000_000m);
        b.Statemented(Oct10, -2_000_000m);
        await new AutoMatchBankStatementCommandHandler(b.Uow.Object).Handle(new AutoMatchBankStatementCommand { BankStatementId = b.Statement.Id }, default);

        var dto = await new GetBankReconciliationQueryHandler(b.Uow.Object).Handle(new GetBankReconciliationQuery { BankStatementId = b.Statement.Id }, default);

        (dto.LedgerBalance, dto.OutstandingLedgerNet, dto.ExpectedClosing, dto.Difference, dto.CanReconcile).Should().Be((2_000_000m, -1_000_000m, 3_000_000m, 0m, true));
        dto.UnmatchedLedger.Should().ContainSingle().Which.EntryNumber.Should().Be("JE-3");
    }

    [Fact]
    public async Task A_statement_line_the_books_do_not_know_blocks_the_reconciliation_until_it_is_dealt_with()
    {
        var b = new Books(0m, 2_950_000m);
        b.Ledgered("JE-1", Oct4, 5_000_000m, 0m);
        b.Ledgered("JE-2", Oct10, 0m, 2_000_000m);
        b.Statemented(Oct4, 5_000_000m);
        b.Statemented(Oct10, -2_000_000m);
        b.Statemented(Oct15, -50_000m, "FEE");
        var dto = await new AutoMatchBankStatementCommandHandler(b.Uow.Object).Handle(new AutoMatchBankStatementCommand { BankStatementId = b.Statement.Id }, default);

        (dto.UnmatchedStatementLines, dto.UnmatchedStatementNet, dto.Difference, dto.CanReconcile).Should().Be((1, -50_000m, 0m, false));

        var act = () => new ReconcileBankStatementCommandHandler(b.Uow.Object).Handle(new ReconcileBankStatementCommand { BankStatementId = b.Statement.Id }, default);
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*not matched*");
    }

    [Fact]
    public async Task A_closing_balance_the_books_cannot_explain_shows_the_difference_and_is_refused()
    {
        var b = new Books(0m, 3_100_000m);                       // the bank says 100,000 more than the books
        b.Ledgered("JE-1", Oct4, 5_000_000m, 0m);
        b.Ledgered("JE-2", Oct10, 0m, 2_000_000m);
        b.Statemented(Oct4, 5_000_000m);
        b.Statemented(Oct10, -2_000_000m);
        var dto = await new AutoMatchBankStatementCommandHandler(b.Uow.Object).Handle(new AutoMatchBankStatementCommand { BankStatementId = b.Statement.Id }, default);

        (dto.Difference, dto.CanReconcile).Should().Be((100_000m, false));
        dto.Notes.Should().Contain(n => n.Contains("line is missing or wrong"));
        var act = () => new ReconcileBankStatementCommandHandler(b.Uow.Object).Handle(new ReconcileBankStatementCommand { BankStatementId = b.Statement.Id }, default);
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*difference is 100,000*");
    }

    [Fact]
    public async Task Ledger_lines_before_the_first_statement_line_are_taken_as_cleared()
    {
        var b = new Books(1_000_000m, 3_000_000m);               // the bank held 1,000,000 before the first line
        b.Ledgered("JE-0", new DateTime(2026, 9, 1), 1_000_000m, 0m);
        b.Ledgered("JE-1", Oct4, 2_000_000m, 0m);
        b.Statemented(Oct4, 2_000_000m);
        var dto = await new AutoMatchBankStatementCommandHandler(b.Uow.Object).Handle(new AutoMatchBankStatementCommand { BankStatementId = b.Statement.Id }, default);

        (dto.LedgerBalance, dto.OutstandingLedgerNet, dto.Difference, dto.CanReconcile).Should().Be((3_000_000m, 0m, 0m, true));
    }

    [Fact]
    public async Task A_manual_match_needs_the_same_amount_and_a_ledger_line_nobody_else_took_and_can_be_undone()
    {
        var b = new Books(0m, 100m);
        var ledger = b.Ledgered("JE-1", Oct4, 100m, 0m);
        var other = b.Ledgered("JE-2", Oct4, 90m, 0m);
        var line = b.Statemented(Oct4, 100m);
        var match = new MatchBankStatementLineCommandHandler(b.Uow.Object);

        var wrongAmount = () => match.Handle(new MatchBankStatementLineCommand { LineId = line.Id, JournalLineId = other.LineId }, default);
        await wrongAmount.Should().ThrowAsync<BusinessRuleException>().WithMessage("*amounts differ*");
        var unknown = () => match.Handle(new MatchBankStatementLineCommand { LineId = line.Id, JournalLineId = Guid.NewGuid() }, default);
        await unknown.Should().ThrowAsync<BusinessRuleException>().WithMessage("*not on the bank's ledger account*");

        var ok = await match.Handle(new MatchBankStatementLineCommand { LineId = line.Id, JournalLineId = ledger.LineId }, default);
        ok.Lines.Single().MatchStatus.Should().Be("Matched");
        ok.Lines.Single().MatchedEntryNumber.Should().Be("JE-1");

        var second = b.Statemented(Oct4, 100m);
        var taken = () => match.Handle(new MatchBankStatementLineCommand { LineId = second.Id, JournalLineId = ledger.LineId }, default);
        await taken.Should().ThrowAsync<BusinessRuleException>().WithMessage("*already matched*");

        var undone = await new UnmatchBankStatementLineCommandHandler(b.Uow.Object).Handle(new UnmatchBankStatementLineCommand { LineId = line.Id }, default);
        undone.Lines.First(l => l.Id == line.Id).MatchStatus.Should().Be("Unmatched");
    }

    [Fact]
    public async Task Importing_a_csv_adds_the_lines_and_refuses_everything_when_a_row_is_wrong_or_repeated()
    {
        var b = new Books(0m, 0m);
        var import = new ImportBankStatementLinesCommandHandler(b.Uow.Object);

        var ok = await import.Handle(new ImportBankStatementLinesCommand { BankStatementId = b.Statement.Id, Csv = "Date,Description,Reference,Amount\n2026-10-04,A,R1,100\n2026-10-05,B,R2,-40" }, default);
        ok.Imported.Should().Be(2);
        ok.Errors.Should().BeEmpty();

        var repeated = await import.Handle(new ImportBankStatementLinesCommand { BankStatementId = b.Statement.Id, Csv = "Date,Description,Reference,Amount\n2026-10-04,A,R1,100\n2026-10-06,C,R3,5" }, default);
        repeated.Imported.Should().Be(0);
        repeated.Errors.Should().ContainSingle().Which.Message.Should().Contain("already on this account");

        var bad = await import.Handle(new ImportBankStatementLinesCommand { BankStatementId = b.Statement.Id, Csv = "Date,Amount\nnope,1" }, default);
        bad.Imported.Should().Be(0);
        b.Lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_bank_account_without_a_ledger_account_cannot_be_matched_or_reconciled_by_line()
    {
        var b = new Books(0m, 100m, withGl: false);
        b.Statemented(Oct4, 100m);

        var auto = () => new AutoMatchBankStatementCommandHandler(b.Uow.Object).Handle(new AutoMatchBankStatementCommand { BankStatementId = b.Statement.Id }, default);
        await auto.Should().ThrowAsync<BusinessRuleException>().WithMessage("*no ledger account*");
        var reconcile = () => new ReconcileBankStatementCommandHandler(b.Uow.Object).Handle(new ReconcileBankStatementCommand { BankStatementId = b.Statement.Id }, default);
        await reconcile.Should().ThrowAsync<BusinessRuleException>().WithMessage("*no ledger account*");
    }

    [Fact]
    public async Task A_statement_without_lines_is_reconciled_as_before()
    {
        var b = new Books(0m, 100m);

        var dto = await new ReconcileBankStatementCommandHandler(b.Uow.Object).Handle(new ReconcileBankStatementCommand { BankStatementId = b.Statement.Id }, default);

        dto.Status.Should().Be("Reconciled");
    }

    [Fact]
    public async Task A_line_matched_to_a_ledger_line_dated_after_the_statement_still_counts()
    {
        var b = new Books(0m, 100m);
        b.Ledgered("JE-1", new DateTime(2026, 11, 2), 100m, 0m);       // booked two days after the statement date
        b.Statemented(new DateTime(2026, 10, 31), 100m);

        var dto = await new AutoMatchBankStatementCommandHandler(b.Uow.Object).Handle(new AutoMatchBankStatementCommand { BankStatementId = b.Statement.Id }, default);

        (dto.Lines.Single().MatchStatus, dto.LedgerBalance, dto.Difference).Should().Be(("Matched", 100m, 0m));
    }

    // ---- fees and interest posted from a statement line ----

    private static (PostBankStatementLineCommandHandler Handler, List<PostingRequest> Posted, Dictionary<string, Guid> Accounts) PostingHandler(Books b, bool postingWorks = true)
    {
        var accounts = new Dictionary<string, Guid>
        {
            [PostingKeys.BankFee] = Guid.NewGuid(), [PostingKeys.BankInterestIncome] = Guid.NewGuid(), [PostingKeys.InterestExpense] = Guid.NewGuid(),
        };
        var posted = new List<PostingRequest>();
        var posting = new Mock<IJournalPostingService>();
        posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostingRequest r, CancellationToken _) =>
            {
                if (!postingWorks) throw new ERP.ACC.Domain.PostingConfigurationException("BankFee", "no account");
                posted.Add(r);
                return JournalEntry.CreatePosted("JE-BANK", Guid.NewGuid(), r.Date, r.Description, r.SourceType, r.SourceId,
                    r.Lines.Select(l => new JournalLineDraft(l.AccountId ?? accounts[l.PostingKey!], l.Debit, l.Credit, l.Description)));
            });
        return (new PostBankStatementLineCommandHandler(b.Uow.Object, posting.Object, NullLogger<PostBankStatementLineCommandHandler>.Instance), posted, accounts);
    }

    [Fact]
    public async Task A_bank_fee_line_is_posted_against_the_bank_account_and_the_statement_can_then_be_reconciled()
    {
        var b = new Books(0m, 2_950_000m);
        b.Ledgered("JE-1", Oct4, 5_000_000m, 0m);
        b.Ledgered("JE-2", Oct10, 0m, 2_000_000m);
        b.Statemented(Oct4, 5_000_000m);
        b.Statemented(Oct10, -2_000_000m);
        var fee = b.Statemented(Oct15, -50_000m, "FEE");
        await new AutoMatchBankStatementCommandHandler(b.Uow.Object).Handle(new AutoMatchBankStatementCommand { BankStatementId = b.Statement.Id }, default);
        var (handler, posted, _) = PostingHandler(b);

        var dto = await handler.Handle(new PostBankStatementLineCommand { LineId = fee.Id }, default);

        var request = posted.Single();
        request.SourceId.Should().Be(fee.Id);
        request.Date.Should().Be(Oct15);
        var bank = request.Lines.Single(l => l.AccountId == b.GlAccountId);
        (bank.Debit, bank.Credit).Should().Be((0m, 50_000m));
        request.Lines.Single(l => l.PostingKey == PostingKeys.BankFee).Debit.Should().Be(50_000m);
        fee.MatchStatus.Should().Be(BankStatementLine.Posted);
        fee.JournalEntryId.Should().NotBeNull();
        dto.Lines.Single(l => l.Id == fee.Id).MatchStatus.Should().Be("Posted");
        dto.UnmatchedStatementLines.Should().Be(0);
    }

    [Fact]
    public async Task A_deposit_line_defaults_to_interest_income_and_an_account_can_be_chosen()
    {
        var b = new Books(0m, 0m);
        var interest = b.Statemented(Oct15, 1_250m, "INT");
        var direct = b.Statemented(Oct15, -300m);
        var (handler, posted, _) = PostingHandler(b);
        var chosen = Guid.NewGuid();

        await handler.Handle(new PostBankStatementLineCommand { LineId = interest.Id }, default);
        await handler.Handle(new PostBankStatementLineCommand { LineId = direct.Id, AccountId = chosen, Description = "Direct debit" }, default);

        var first = posted[0];
        first.Lines.Single(l => l.AccountId == b.GlAccountId).Debit.Should().Be(1_250m);
        first.Lines.Single(l => l.PostingKey == PostingKeys.BankInterestIncome).Credit.Should().Be(1_250m);
        var second = posted[1];
        second.Lines.Single(l => l.AccountId == chosen).Debit.Should().Be(300m);
        second.Description.Should().Contain("Direct debit");
    }

    [Fact]
    public async Task A_line_cannot_be_posted_twice_against_the_bank_account_itself_or_with_an_unknown_key_or_without_a_profile()
    {
        var b = new Books(0m, 0m);
        var line = b.Statemented(Oct15, -50_000m);
        var (handler, _, _) = PostingHandler(b);

        var own = () => handler.Handle(new PostBankStatementLineCommand { LineId = line.Id, AccountId = b.GlAccountId }, default);
        await own.Should().ThrowAsync<BusinessRuleException>().WithMessage("*own ledger account*");
        var key = () => handler.Handle(new PostBankStatementLineCommand { LineId = line.Id, PostingKey = "Cash" }, default);
        await key.Should().ThrowAsync<BusinessRuleException>().WithMessage("*not an account for a bank statement line*");

        var (broken, _, _) = PostingHandler(b, postingWorks: false);
        var noProfile = () => broken.Handle(new PostBankStatementLineCommand { LineId = line.Id }, default);
        await noProfile.Should().ThrowAsync<BusinessRuleException>().WithMessage("*posting profile*");
        line.MatchStatus.Should().Be(BankStatementLine.Unmatched);

        await handler.Handle(new PostBankStatementLineCommand { LineId = line.Id }, default);
        var again = () => handler.Handle(new PostBankStatementLineCommand { LineId = line.Id }, default);
        await again.Should().ThrowAsync<BusinessRuleException>().WithMessage("*already matched or posted*");
        var unmatch = () => new UnmatchBankStatementLineCommandHandler(b.Uow.Object).Handle(new UnmatchBankStatementLineCommand { LineId = line.Id }, default);
        await unmatch.Should().ThrowAsync<BusinessRuleException>().WithMessage("*reverse that entry*");
    }

    [Fact]
    public async Task A_bank_account_without_a_ledger_account_cannot_post_a_line()
    {
        var b = new Books(0m, 0m, withGl: false);
        var line = b.Statemented(Oct15, -50_000m);
        var (handler, _, _) = PostingHandler(b);

        var act = () => handler.Handle(new PostBankStatementLineCommand { LineId = line.Id }, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*no ledger account*");
    }

    [Fact]
    public void The_bank_keys_have_default_accounts_on_both_charts()
    {
        PostingKeys.All.Should().Contain(new[] { PostingKeys.BankFee, PostingKeys.BankInterestIncome });
        PostingKeys.DefaultAccountNumbers.Should().ContainKeys(PostingKeys.BankFee, PostingKeys.BankInterestIncome);
        var vas = ChartTemplates.All.Single(x => x.Id == ChartTemplates.Vas99);
        (vas.PostingMap[PostingKeys.BankFee], vas.PostingMap[PostingKeys.BankInterestIncome]).Should().Be(("6427", "515"));
    }
}
