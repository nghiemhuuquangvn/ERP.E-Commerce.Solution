using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Reports;
using ERP.ACC.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Reports;

/// <summary>ROADMAP P1-3: the VAT listing and the simplified return 01/GTGT.</summary>
public class VatReportTests
{
    private static readonly DateTime From = new(2026, 10, 1);
    private static readonly DateTime To = new(2026, 10, 31);
    private static DateTime In(int day) => new(2026, 10, day);

    private readonly Account _input = Account.Create("1331", "Input VAT", 1, 1);
    private readonly Account _output = Account.Create("3331", "Output VAT", 2, 2);
    private readonly List<Invoice> _invoices = new();
    private readonly List<Bill> _bills = new();
    private readonly List<LedgerRow> _ledger = new();
    private decimal _carriedDebit;

    private static Invoice Inv(string number, decimal net, decimal rate, decimal tax, int day, bool cancelled = false)
    {
        var i = Invoice.Create(number, Guid.NewGuid(), Guid.NewGuid(), net + tax, In(day).AddDays(30));
        i.ApplyVat(net, rate, tax);
        i.CreatedAt = In(day);
        if (cancelled) i.Cancel();
        return i;
    }

    private static Bill Bll(string number, decimal net, decimal rate, decimal tax, int day)
    {
        var b = Bill.Create(number, Guid.NewGuid(), Guid.NewGuid(), net + tax, In(day).AddDays(30));
        b.ApplyVat(net, rate, tax);
        b.Approve();
        b.ApprovedOn = In(day);
        return b;
    }

    private static LedgerRow Line(Account account, string type, string number, int day, decimal debit, decimal credit) =>
        new() { AccountId = account.Id, ReferenceType = type, EntryNumber = number, EntryDate = In(day), EntryDescription = type + " " + number, Debit = debit, Credit = credit, Status = 1 };

    private GetVatListingQueryHandler Listing()
    {
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Account> { _input, _output });
        var mappings = new Mock<IPostingAccountMappingRepository>();
        mappings.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<PostingAccountMapping>());
        var invoices = new Mock<IInvoiceRepository>();
        invoices.Setup(i => i.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _invoices.ToList());
        var bills = new Mock<IBillRepository>();
        bills.Setup(b => b.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _bills.ToList());
        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(l => l.GetLedgerLinesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, DateTime? f, DateTime? t, CancellationToken _) => _ledger.Where(r => ids.Contains(r.AccountId) && r.EntryDate >= f && r.EntryDate <= t).ToList());
        lines.Setup(l => l.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new List<AccountBalanceSummary> { new() { AccountId = _input.Id, TotalDebit = _carriedDebit, TotalCredit = 0m } });
        var profiles = new Mock<IReportProfileRepository>();
        return new GetVatListingQueryHandler(accounts.Object, mappings.Object, invoices.Object, bills.Object, lines.Object, profiles.Object);
    }

    private GetVatReturnQueryHandler Return(GetVatListingQueryHandler listing)
    {
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Account> { _input, _output });
        var mappings = new Mock<IPostingAccountMappingRepository>();
        mappings.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<PostingAccountMapping>());
        var lines = new Mock<IJournalLineRepository>();
        lines.Setup(l => l.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new List<AccountBalanceSummary> { new() { AccountId = _input.Id, TotalDebit = _carriedDebit, TotalCredit = 0m } });
        var profiles = new Mock<IReportProfileRepository>();
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetVatListingQuery>(), It.IsAny<CancellationToken>()))
            .Returns((GetVatListingQuery q, CancellationToken ct) => listing.Handle(q, ct));
        return new GetVatReturnQueryHandler(mediator.Object, accounts.Object, mappings.Object, lines.Object, profiles.Object);
    }

    private void SalesMonth()
    {
        _invoices.Add(Inv("INV-1", 1_000_000m, 10m, 100_000m, 5));
        _invoices.Add(Inv("INV-2", 500_000m, 8m, 40_000m, 12));
        _invoices.Add(Inv("INV-C", 900_000m, 10m, 90_000m, 14, cancelled: true));        // cancelled: not listed
        _invoices.Add(Inv("INV-OLD", 100_000m, 10m, 10_000m, 5));
        _invoices.Last().CreatedAt = new DateTime(2026, 9, 5);                            // before the period
        _ledger.Add(Line(_output, "Invoice", "JE-1", 5, 0m, 100_000m));
        _ledger.Add(Line(_output, "Invoice", "JE-2", 12, 0m, 40_000m));
        _ledger.Add(Line(_output, "SalesReturn", "JE-3", 20, 50_000m, 0m));               // a return
        _ledger.Add(Line(_output, "PosSale", "JE-4", 22, 0m, 20_000m));                   // a POS sale
    }

    [Fact]
    public async Task The_sales_listing_has_the_invoices_of_the_period_and_the_other_ledger_movements_and_agrees_with_the_account()
    {
        SalesMonth();

        var listing = await Listing().Handle(new GetVatListingQuery { FromDate = From, ToDate = To, Direction = "output" }, default);

        listing.Rows.Select(r => r.Number).Should().Equal("INV-1", "INV-2", "JE-3", "JE-4");
        listing.Rows.Where(r => r.Kind == "Entry").Select(r => r.Tax).Should().Equal(-50_000m, 20_000m);
        listing.TotalTax.Should().Be(110_000m);
        listing.LedgerTax.Should().Be(110_000m);
        listing.IsReconciled.Should().BeTrue();
        listing.ByRate.Single(g => g.RatePercent == 10m).Should().BeEquivalentTo(new VatRateTotalDto { RatePercent = 10m, Net = 1_000_000m, Tax = 100_000m });
        listing.ByRate.Single(g => g.RatePercent == 8m).Tax.Should().Be(40_000m);
        listing.ByRate.Single(g => g.RatePercent == null).Tax.Should().Be(-30_000m);
        listing.LedgerAccountNumber.Should().Be("3331");
    }

    [Fact]
    public async Task A_difference_between_the_listing_and_the_ledger_is_reported()
    {
        SalesMonth();
        _ledger.Add(Line(_output, "Invoice", "JE-9", 25, 0m, 7_000m));   // an invoice entry whose document is not in the listing

        var listing = await Listing().Handle(new GetVatListingQuery { FromDate = From, ToDate = To, Direction = "output" }, default);

        listing.Difference.Should().Be(-7_000m);
        listing.IsReconciled.Should().BeFalse();
        listing.Notes.Should().Contain(n => n.Contains("differs"));
    }

    [Fact]
    public async Task The_purchase_listing_has_the_approved_bills_by_their_approval_date()
    {
        _bills.Add(Bll("BILL-1", 2_000_000m, 10m, 200_000m, 8));
        _bills.Add(Bll("BILL-2", 100_000m, 0m, 0m, 15));
        _bills.Add(Bll("BILL-OUT", 300_000m, 10m, 30_000m, 3));
        _bills.Last().ApprovedOn = new DateTime(2026, 11, 3);
        _bills.Add(Bill.Create("BILL-P", Guid.NewGuid(), Guid.NewGuid(), 1100m, In(30)));      // pending: not a deduction yet
        _ledger.Add(Line(_input, "Bill", "JE-5", 8, 200_000m, 0m));

        var listing = await Listing().Handle(new GetVatListingQuery { FromDate = From, ToDate = To, Direction = "input" }, default);

        listing.Rows.Select(r => r.Number).Should().Equal("BILL-1", "BILL-2");
        listing.TotalNet.Should().Be(2_100_000m);
        listing.TotalTax.Should().Be(200_000m);
        listing.IsReconciled.Should().BeTrue();
    }

    [Fact]
    public async Task An_unknown_direction_or_a_reversed_range_is_refused()
    {
        var handler = Listing();

        await handler.Invoking(h => h.Handle(new GetVatListingQuery { FromDate = From, ToDate = To, Direction = "both" }, default)).Should().ThrowAsync<BusinessRuleException>();
        await handler.Invoking(h => h.Handle(new GetVatListingQuery { FromDate = To, ToDate = From, Direction = "output" }, default)).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task The_return_carries_the_excess_input_VAT_forward()
    {
        SalesMonth();                                            // output VAT 110,000
        _bills.Add(Bll("BILL-1", 2_000_000m, 10m, 200_000m, 8));
        _ledger.Add(Line(_input, "Bill", "JE-5", 8, 200_000m, 0m));
        _carriedDebit = 30_000m;

        var vat = await Return(Listing()).Handle(new GetVatReturnQuery { FromDate = From, ToDate = To }, default);

        decimal Amount(string code) => vat.Rows.Single(r => r.Code == code).Amount;
        Amount("22").Should().Be(30_000m);
        Amount("23").Should().Be(2_000_000m);
        Amount("24").Should().Be(200_000m);
        Amount("25").Should().Be(230_000m);
        Amount("S10").Should().Be(1_000_000m);
        Amount("T10").Should().Be(100_000m);
        Amount("T8").Should().Be(40_000m);
        Amount("35").Should().Be(110_000m);
        Amount("40").Should().Be(0m);
        Amount("43").Should().Be(120_000m);
        vat.IsDraft.Should().BeTrue();
        vat.Notes.Should().Contain(n => n.Contains("agrees"));
    }

    [Fact]
    public async Task The_return_shows_the_VAT_payable_and_converts_to_the_report_unit()
    {
        SalesMonth();
        _ledger.Add(Line(_input, "Manual", "JE-6", 9, 10_000m, 0m));        // input VAT booked by hand, no bill

        var vat = await Return(Listing()).Handle(new GetVatReturnQuery { FromDate = From, ToDate = To, Unit = "Thousand" }, default);

        vat.Unit.Should().Be("thousand VND");
        vat.Rows.Single(r => r.Code == "24").Amount.Should().Be(10m);        // 10,000 -> 10 thousand
        vat.Rows.Single(r => r.Code == "40").Amount.Should().Be(100m);       // 110,000 − 10,000 = 100,000
        vat.Rows.Single(r => r.Code == "43").Amount.Should().Be(0m);
    }

    [Fact]
    public void The_listing_table_has_a_header_row_subtotals_and_the_ledger_check()
    {
        var dto = new VatListingDto
        {
            Direction = "output", FromDate = From, ToDate = To, TotalNet = 1000m, TotalTax = 100m, LedgerTax = 100m, IsReconciled = true, LedgerAccountNumber = "3331",
            Rows = { new VatListingRowDto { Kind = "Invoice", Number = "INV-1", Date = In(5), Net = 1000m, RatePercent = 10m, Tax = 100m } },
            ByRate = { new VatRateTotalDto { RatePercent = 10m, Net = 1000m, Tax = 100m } },
        };

        var table = ReportTables.VatListing(dto);

        table.Rows[table.HeaderRow - 1].Should().Contain("Net");
        table.Rows.SelectMany(r => r).OfType<string>().Should().Contain("Subtotal").And.Contain("reconciled");
        ERP.ACC.Application.Reports.XlsxWriter.ToBytes(table).Length.Should().BeGreaterThan(1000);
    }
}
