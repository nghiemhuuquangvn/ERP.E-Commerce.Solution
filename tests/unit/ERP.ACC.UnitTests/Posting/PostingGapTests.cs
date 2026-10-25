using System.Xml.Linq;
using ERP.ACC.Application.Commands;
using ERP.ACC.Application.Handlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Reports;
using ERP.ACC.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Posting;

/// <summary>ROADMAP P1-3b: the warning when a document could not be posted, the list of such documents, their repost, and the XML of the VAT return.</summary>
public class PostingGapTests
{
    private readonly List<Invoice> _invoices = new();
    private readonly List<Bill> _bills = new();
    private readonly HashSet<string> _entries = new();
    private bool _periodOpen;

    private Mock<IJournalPostingService> Posting()
    {
        var posting = new Mock<IJournalPostingService>();
        posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((PostingRequest r, CancellationToken _) =>
        {
            if (!_periodOpen)
                return null;                                 // TryPostAsync turns the configuration error into null
            _entries.Add(JournalEntry.BuildSourceKey(r.SourceType, r.SourceId));
            return JournalEntry.Create($"JE-{_entries.Count}", Guid.NewGuid(), r.Description);
        });
        return posting;
    }

    private Mock<IAccountingUnitOfWork> Uow()
    {
        var invoices = new Mock<IInvoiceRepository>();
        invoices.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _invoices.ToList());
        invoices.Setup(r => r.AddAsync(It.IsAny<Invoice>(), It.IsAny<CancellationToken>())).Callback<Invoice, CancellationToken>((i, _) => _invoices.Add(i)).Returns(Task.CompletedTask);
        var bills = new Mock<IBillRepository>();
        bills.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _bills.ToList());
        bills.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) => _bills.FirstOrDefault(b => b.Id == id));
        var journal = new Mock<IJournalEntryRepository>();
        journal.Setup(j => j.ExistingSourceKeysAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string> keys, CancellationToken _) => keys.Where(_entries.Contains).ToHashSet());
        var uow = new Mock<IAccountingUnitOfWork>();
        uow.SetupGet(u => u.Invoices).Returns(invoices.Object);
        uow.SetupGet(u => u.Bills).Returns(bills.Object);
        uow.SetupGet(u => u.JournalEntries).Returns(journal.Object);
        return uow;
    }

    [Fact]
    public async Task A_document_that_could_not_be_posted_carries_a_warning_and_a_posted_one_does_not()
    {
        var uow = Uow();
        var handler = new CreateInvoiceCommandHandler(uow.Object, Posting().Object, NullLogger<CreateInvoiceCommandHandler>.Instance);
        var command = new CreateInvoiceCommand { InvoiceNumber = "INV-G1", CustomerId = Guid.NewGuid(), OrderId = Guid.NewGuid(), Amount = 1000m, DueDate = DateTime.UtcNow.AddDays(30) };

        var closed = await handler.Handle(command, default);
        _periodOpen = true;
        command.InvoiceNumber = "INV-G2";
        command.OrderId = Guid.NewGuid();
        var open = await handler.Handle(command, default);

        (closed.PostedToLedger, closed.PostingWarning).Should().Be((false, PostingWarnings.NotPosted));
        (open.PostedToLedger, open.PostingWarning).Should().Be((true, null));
    }

    [Fact]
    public async Task The_gaps_list_the_invoices_and_approved_bills_without_an_entry_and_skip_cancelled_and_pending_ones()
    {
        var uow = Uow();
        var posted = Invoice.Create("INV-A", Guid.NewGuid(), Guid.NewGuid(), 500m, DateTime.UtcNow);
        var missing = Invoice.Create("INV-B", Guid.NewGuid(), Guid.NewGuid(), 700m, DateTime.UtcNow);
        var cancelled = Invoice.Create("INV-C", Guid.NewGuid(), Guid.NewGuid(), 900m, DateTime.UtcNow);
        cancelled.Cancel();
        var approved = Bill.Create("BILL-A", Guid.NewGuid(), Guid.NewGuid(), 1200m, DateTime.UtcNow);
        approved.Approve();
        var pending = Bill.Create("BILL-P", Guid.NewGuid(), Guid.NewGuid(), 300m, DateTime.UtcNow);
        _invoices.AddRange(new[] { posted, missing, cancelled });
        _bills.AddRange(new[] { approved, pending });
        _entries.Add(JournalEntry.BuildSourceKey(PostingRules.Invoice, posted.Id));

        var gaps = await new GetPostingGapsQueryHandler(uow.Object).Handle(new GetPostingGapsQuery(), default);

        gaps.Select(g => g.Number).Should().BeEquivalentTo("INV-B", "BILL-A");
        gaps.Single(g => g.Number == "BILL-A").DocumentType.Should().Be("Bill");
    }

    [Fact]
    public async Task Reposting_posts_each_missing_entry_once_and_reports_the_ones_that_still_cannot_be_posted()
    {
        var uow = Uow();
        _invoices.Add(Invoice.Create("INV-B", Guid.NewGuid(), Guid.NewGuid(), 700m, DateTime.UtcNow));
        var handler = new RepostPostingGapsCommandHandler(uow.Object, Posting().Object, NullLogger<RepostPostingGapsCommandHandler>.Instance);

        var stillClosed = await handler.Handle(new RepostPostingGapsCommand(), default);
        _periodOpen = true;
        var reposted = await handler.Handle(new RepostPostingGapsCommand(), default);
        var again = await handler.Handle(new RepostPostingGapsCommand(), default);

        stillClosed.Single().Posted.Should().BeFalse();
        (reposted.Single().Posted, reposted.Single().EntryNumber).Should().Be((true, "JE-1"));
        again.Should().BeEmpty();                                         // nothing missing any more
    }

    // ---- XML ----

    [Fact]
    public void The_XML_of_the_return_is_well_formed_carries_the_taxpayer_the_period_and_every_indicator()
    {
        var vat = new FinancialStatementDto
        {
            FromDate = new DateTime(2026, 10, 1), ToDate = new DateTime(2026, 10, 31), Unit = "VND",
            Header = new ReportHeaderDto { CompanyName = "Mau Co Ltd", TaxCode = "0312345678", Address = "1 Le Loi", ChiefAccountant = "Tran B" },
            Rows =
            {
                new StatementRowDto { Code = "22", Name = "carried", Amount = 30_000m },
                new StatementRowDto { Code = "35", Name = "output", Amount = 110_000.5m },
                new StatementRowDto { Code = "S10", Name = "sales 10", Amount = 1_000_000m },
            },
        };

        var doc = XDocument.Parse(System.Text.Encoding.UTF8.GetString(VatReturnXml.Build(vat)));

        XNamespace ns = VatReturnXml.Namespace;
        doc.Root!.Name.Should().Be(ns + "HSoThueDTu");
        doc.Descendants(ns + "mst").Single().Value.Should().Be("0312345678");
        doc.Descendants(ns + "kyKKhai").Single().Value.Should().Be("10/2026");
        doc.Descendants(ns + "kyKKhaiTuNgay").Single().Value.Should().Be("01/10/2026");
        doc.Descendants(ns + "ct22").Single().Value.Should().Be("30000");
        doc.Descendants(ns + "ct35").Single().Value.Should().Be("110000.5");
        doc.Descendants(ns + "ctS10").Single().Value.Should().Be("1000000");
        doc.Descendants(ns + "nguoiKy").Single().Value.Should().Be("Tran B");
    }

    [Fact]
    public void A_quarter_is_declared_as_a_quarter()
    {
        var vat = new FinancialStatementDto { FromDate = new DateTime(2026, 7, 1), ToDate = new DateTime(2026, 9, 30), Unit = "VND" };

        var doc = XDocument.Parse(System.Text.Encoding.UTF8.GetString(VatReturnXml.Build(vat)));

        XNamespace ns = VatReturnXml.Namespace;
        (doc.Descendants(ns + "kieuKy").Single().Value, doc.Descendants(ns + "kyKKhai").Single().Value).Should().Be(("Q", "3/2026"));
    }
}
