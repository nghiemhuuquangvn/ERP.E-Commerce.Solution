using ERP.ACC.Application.Commands;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Reports;
using ERP.ACC.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Posting;

/// <summary>ROADMAP P1-3b: several VAT rates and goods not subject to VAT on one document, and the return that follows them.</summary>
public class VatLineTests
{
    private static Dictionary<string, (decimal Dr, decimal Cr)> ByKey(PostingRequest r) =>
        r.Lines.GroupBy(l => l.PostingKey!).ToDictionary(g => g.Key, g => (g.Sum(l => l.Debit), g.Sum(l => l.Credit)));

    private static List<VatLineInput> Mixed() => new()
    {
        new() { Category = 0, RatePercent = 10m, Amount = 1_100_000m, Description = "Goods 10 %" },
        new() { Category = 0, RatePercent = 8m, Amount = 540_000m, Description = "Goods 8 %" },
        new() { Category = 1, Amount = 300_000m, Description = "Books" },
    };

    // ---- calculator ----

    [Fact]
    public void Mixed_lines_add_up_and_each_line_is_split_by_its_own_rate()
    {
        var split = VatCalculator.SplitLines(Mixed(), pricesIncludeTax: true);

        split.Lines.Select(l => (l.Net, l.Tax)).Should().Equal((1_000_000m, 100_000m), (500_000m, 40_000m), (300_000m, 0m));
        (split.Net, split.Tax, split.Gross).Should().Be((1_800_000m, 140_000m, 1_940_000m));
        (split.Net + split.Tax).Should().Be(split.Gross);
        split.SummaryRate.Should().Be(0m);                     // mixed rates: no single rate to show
    }

    [Fact]
    public void With_prices_excluding_tax_each_line_adds_its_own_VAT()
    {
        var split = VatCalculator.SplitLines(new[] { new VatLineInput { Category = 0, RatePercent = 10m, Amount = 1_000_000m }, new VatLineInput { Category = 1, Amount = 100_000m } }, false);

        (split.Net, split.Tax, split.Gross).Should().Be((1_100_000m, 100_000m, 1_200_000m));
        split.SummaryRate.Should().Be(10m);                    // one taxable rate (the goods not subject to VAT do not count)
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(0, 150, 100)]
    [InlineData(2, 10, 100)]
    public void A_bad_line_is_refused(int category, int rate, int amount)
    {
        var act = () => VatCalculator.SplitLines(new[] { new VatLineInput { Category = category, RatePercent = rate, Amount = amount } }, true);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void No_lines_are_refused()
    {
        var act = () => VatCalculator.SplitLines(new List<VatLineInput>(), true);

        act.Should().Throw<ArgumentException>();
    }

    // ---- handlers ----

    private readonly List<Invoice> _invoices = new();
    private readonly List<Bill> _bills = new();
    private readonly List<VatLine> _lines = new();
    private readonly List<PostingRequest> _posted = new();

    private (CreateInvoiceCommandHandler Create, AdjustInvoiceVatCommandHandler Adjust, CreateBillCommandHandler CreateBill, ApproveBillCommandHandler Approve, GetVatLinesQueryHandler Query) Handlers(bool inclusive = true)
    {
        var invoices = new Mock<IInvoiceRepository>();
        invoices.Setup(r => r.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>())).Callback<Invoice, CancellationToken>((i, _) => _invoices.Add(i)).Returns(Task.CompletedTask);
        invoices.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) => _invoices.FirstOrDefault(i => i.Id == id));
        var bills = new Mock<IBillRepository>();
        bills.Setup(r => r.AddAsync(It.IsAny<Bill>(), It.IsAny<CancellationToken>())).Callback<Bill, CancellationToken>((b, _) => _bills.Add(b)).Returns(Task.CompletedTask);
        bills.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) => _bills.FirstOrDefault(b => b.Id == id));
        var uow = new Mock<IAccountingUnitOfWork>();
        uow.SetupGet(u => u.Invoices).Returns(invoices.Object);
        uow.SetupGet(u => u.Bills).Returns(bills.Object);
        var posting = new Mock<IJournalPostingService>();
        posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PostingRequest, CancellationToken>((r, _) => _posted.Add(r)).ReturnsAsync(() => JournalEntry.Create("JE-1", Guid.NewGuid(), "x"));
        var vat = new Mock<IVatPolicy>();
        vat.Setup(v => v.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new VatSettings(10m, inclusive));
        var lines = new Mock<IVatLineRepository>();
        lines.Setup(l => l.ReplaceAsync(It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<IEnumerable<VatLine>>(), It.IsAny<CancellationToken>()))
            .Callback<int, Guid, IEnumerable<VatLine>, CancellationToken>((t, id, ls, _) => { _lines.RemoveAll(x => x.DocumentType == t && x.DocumentId == id); _lines.AddRange(ls); }).Returns(Task.CompletedTask);
        lines.Setup(l => l.ListAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int t, IReadOnlyCollection<Guid> ids, CancellationToken _) => _lines.Where(x => x.DocumentType == t && ids.Contains(x.DocumentId)).ToList());
        return (new CreateInvoiceCommandHandler(uow.Object, posting.Object, NullLogger<CreateInvoiceCommandHandler>.Instance, vat.Object, lines.Object),
                new AdjustInvoiceVatCommandHandler(uow.Object, posting.Object, NullLogger<AdjustInvoiceVatCommandHandler>.Instance, null, lines.Object),
                new CreateBillCommandHandler(uow.Object, vat.Object, lines.Object),
                new ApproveBillCommandHandler(uow.Object, posting.Object, NullLogger<ApproveBillCommandHandler>.Instance, lines.Object),
                new GetVatLinesQueryHandler(uow.Object, lines.Object));
    }

    private static CreateInvoiceCommand Cmd(List<VatLineInput>? lines = null, decimal amount = 0m) =>
        new() { InvoiceNumber = "INV-L", CustomerId = Guid.NewGuid(), OrderId = Guid.NewGuid(), Amount = amount, DueDate = DateTime.UtcNow.AddDays(30), Lines = lines ?? new() };

    [Fact]
    public async Task An_invoice_with_lines_is_the_sum_of_its_lines_and_posts_one_entry_with_the_totals()
    {
        var (create, _, _, _, query) = Handlers();

        var dto = await create.Handle(Cmd(Mixed()), default);

        (dto.Amount, dto.NetAmount, dto.TaxAmount, dto.TaxRatePercent).Should().Be((1_940_000m, 1_800_000m, 140_000m, 0m));
        var lines = ByKey(_posted.Single());
        lines[PostingKeys.AccountsReceivable].Should().Be((1_940_000m, 0m));
        lines[PostingKeys.SalesRevenue].Should().Be((0m, 1_800_000m));
        lines[PostingKeys.VatOutput].Should().Be((0m, 140_000m));
        _lines.Should().HaveCount(3);
        (await query.Handle(new GetVatLinesQuery { DocumentType = 1, DocumentId = dto.Id }, default)).Select(l => l.Category).Should().Equal("Taxable", "Taxable", "NotTaxable");
    }

    [Fact]
    public async Task A_document_without_lines_shows_its_single_implicit_line()
    {
        var (create, _, _, _, query) = Handlers();
        var dto = await create.Handle(Cmd(amount: 1_100_000m), default);

        var shown = await query.Handle(new GetVatLinesQuery { DocumentType = 1, DocumentId = dto.Id }, default);

        _lines.Should().BeEmpty();
        shown.Should().ContainSingle().Which.Should().BeEquivalentTo(new VatLineDto { Category = "Taxable", RatePercent = 10m, Net = 1_000_000m, Tax = 100_000m, Gross = 1_100_000m });
    }

    [Fact]
    public async Task Adjusting_with_lines_keeps_the_gross_posts_the_tax_difference_and_replaces_the_lines()
    {
        var (create, adjust, _, _, _) = Handlers();
        var dto = await create.Handle(Cmd(amount: 1_940_000m), default);       // 10 %: tax 176,363.64
        _posted.Clear();

        var adjusted = await adjust.Handle(new AdjustInvoiceVatCommand { InvoiceId = dto.Id, Lines = Mixed() }, default);

        adjusted.Amount.Should().Be(1_940_000m);
        adjusted.TaxAmount.Should().Be(140_000m);
        ByKey(_posted.Single())[PostingKeys.VatOutput].Should().Be((36_363.64m, 0m));        // VAT output goes down
        _lines.Should().HaveCount(3);
    }

    [Fact]
    public async Task Lines_that_do_not_add_up_to_the_invoice_amount_are_refused_and_nothing_changes()
    {
        var (create, adjust, _, _, _) = Handlers();
        var dto = await create.Handle(Cmd(amount: 1_000_000m), default);

        var act = () => adjust.Handle(new AdjustInvoiceVatCommand { InvoiceId = dto.Id, Lines = Mixed() }, default);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*add up*");
        _invoices.Single().Amount.Should().Be(1_000_000m);
    }

    [Fact]
    public async Task A_single_rate_adjustment_after_lines_drops_the_stored_lines()
    {
        var (create, adjust, _, _, _) = Handlers();
        var dto = await create.Handle(Cmd(Mixed()), default);

        await adjust.Handle(new AdjustInvoiceVatCommand { InvoiceId = dto.Id, TaxRatePercent = 10m }, default);

        _lines.Should().BeEmpty();
    }

    [Fact]
    public async Task A_bill_takes_lines_at_creation_and_at_approval()
    {
        var (_, _, createBill, approve, _) = Handlers();
        var bill = await createBill.Handle(new CreateBillCommand { BillNumber = "B-L", SupplierId = Guid.NewGuid(), POId = Guid.NewGuid(), DueDate = DateTime.UtcNow.AddDays(30), Lines = Mixed() }, default);

        (bill.Amount, bill.TaxAmount).Should().Be((1_940_000m, 140_000m));

        await approve.Handle(new ApproveBillCommand
        {
            BillId = bill.Id,
            Lines = new() { new() { Category = 0, RatePercent = 10m, Amount = 1_940_000m } },
        }, default);

        var entry = ByKey(_posted.Single());
        entry[PostingKeys.VatInput].Should().Be((176_363.64m, 0m));
        _lines.Single().Tax.Should().Be(176_363.64m);
    }

    // ---- return ----

    [Fact]
    public void A_return_takes_the_invoices_VAT_share_so_a_mix_of_rates_is_exact()
    {
        // invoice 1,940,000 with VAT 140,000: a return of 194,000 (10 %) carries 14,000 of VAT
        var tax = Math.Round(140_000m * 194_000m / 1_940_000m, 2);

        var lines = ByKey(PostingRules.ForSalesReturn(Guid.NewGuid(), "INV-L", 194_000m, DateTime.UtcNow, 0m, tax));

        lines[PostingKeys.SalesReturns].Should().Be((180_000m, 0m));
        lines[PostingKeys.VatOutput].Should().Be((14_000m, 0m));
        lines[PostingKeys.AccountsReceivable].Should().Be((0m, 194_000m));
    }

    [Fact]
    public void The_rate_version_of_the_return_is_unchanged()
    {
        ByKey(PostingRules.ForSalesReturn(Guid.NewGuid(), "INV", 550_000m, DateTime.UtcNow, 10m))[PostingKeys.VatOutput].Should().Be((50_000m, 0m));
    }

    // ---- reports ----

    [Fact]
    public async Task The_listing_has_one_row_per_line_and_the_return_shows_the_goods_not_subject_to_VAT()
    {
        var (create, _, _, _, _) = Handlers();
        var dto = await create.Handle(Cmd(Mixed()), default);
        var invoice = _invoices.Single();
        invoice.CreatedAt = new DateTime(2026, 10, 5);

        var account = Account.Create("3331", "Output VAT", 2, 2);
        var input = Account.Create("1331", "Input VAT", 1, 1);
        var accounts = new Mock<IAccountRepository>();
        accounts.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Account> { input, account });
        var mappings = new Mock<IPostingAccountMappingRepository>();
        mappings.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<PostingAccountMapping>());
        var invoiceRepo = new Mock<IInvoiceRepository>();
        invoiceRepo.Setup(i => i.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_invoices.ToList());
        var billRepo = new Mock<IBillRepository>();
        billRepo.Setup(b => b.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Bill>());
        var ledger = new Mock<IJournalLineRepository>();
        ledger.Setup(l => l.GetLedgerLinesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LedgerRow> { new() { AccountId = account.Id, ReferenceType = "Invoice", EntryNumber = "JE-1", EntryDate = new DateTime(2026, 10, 5), Credit = 140_000m, Status = 1 } });
        ledger.Setup(l => l.GetAccountBalancesAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<AccountBalanceSummary>());
        var profiles = new Mock<IReportProfileRepository>();
        var lineRepo = new Mock<IVatLineRepository>();
        lineRepo.Setup(l => l.ListAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int t, IReadOnlyCollection<Guid> ids, CancellationToken _) => _lines.Where(x => x.DocumentType == t && ids.Contains(x.DocumentId)).ToList());
        var listing = new GetVatListingQueryHandler(accounts.Object, mappings.Object, invoiceRepo.Object, billRepo.Object, ledger.Object, profiles.Object, lineRepo.Object);

        var result = await listing.Handle(new GetVatListingQuery { FromDate = new DateTime(2026, 10, 1), ToDate = new DateTime(2026, 10, 31), Direction = "output" }, default);

        result.Rows.Should().HaveCount(3);
        result.Rows.Select(r => r.Category).Should().Equal("Taxable", "Taxable", "NotTaxable");
        result.ByRate.Single(g => g.Category == "NotTaxable").Net.Should().Be(300_000m);
        result.TotalTax.Should().Be(140_000m);
        result.IsReconciled.Should().BeTrue();

        var mediator = new Mock<MediatR.IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetVatListingQuery>(), It.IsAny<CancellationToken>())).Returns((GetVatListingQuery q, CancellationToken ct) => listing.Handle(q, ct));
        var vatReturn = await new GetVatReturnQueryHandler(mediator.Object, accounts.Object, mappings.Object, ledger.Object, profiles.Object)
            .Handle(new GetVatReturnQuery { FromDate = new DateTime(2026, 10, 1), ToDate = new DateTime(2026, 10, 31) }, default);

        decimal Amount(string code) => vatReturn.Rows.Single(r => r.Code == code).Amount;
        Amount("26").Should().Be(300_000m);
        Amount("S10").Should().Be(1_000_000m);
        Amount("T10").Should().Be(100_000m);
        Amount("S8").Should().Be(500_000m);
        Amount("T8").Should().Be(40_000m);
        Amount("35").Should().Be(140_000m);
        dto.Amount.Should().Be(1_940_000m);
    }
}
