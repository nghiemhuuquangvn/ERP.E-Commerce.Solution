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

/// <summary>ROADMAP P1-1c: POS sales, voids and shift variance reach the ledger.</summary>
public class PosPostingTests
{
    private static readonly DateTime Day = new(2026, 10, 24);

    private static PosTenderLine[] Tenders(params (string, decimal)[] t) => t.Select(x => new PosTenderLine(x.Item1, x.Item2)).ToArray();

    private static Dictionary<string, (decimal Dr, decimal Cr)> ByKey(PostingRequest r) =>
        r.Lines.ToDictionary(l => l.PostingKey!, l => (l.Debit, l.Credit));

    [Fact]
    public void A_cash_sale_with_discount_and_tax_splits_into_net_revenue_and_vat()
    {
        // 2 x 100 = 200, discount 20, tax 20 (POS does not reduce tax by the discount) -> total 200
        var request = PostingRules.ForPosSale(Guid.NewGuid(), 200m, 20m, 20m, Tenders(("Cash", 200m)), Day)!;

        request.SourceType.Should().Be("PosSale");
        var lines = ByKey(request);
        lines[PostingKeys.Cash].Should().Be((200m, 0m));
        lines[PostingKeys.SalesRevenue].Should().Be((0m, 180m));
        lines[PostingKeys.VatOutput].Should().Be((0m, 20m));
        request.Lines.Sum(l => l.Debit).Should().Be(request.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public void A_split_sale_debits_cash_and_bank_and_any_non_cash_tender_is_bank()
    {
        var request = PostingRules.ForPosSale(Guid.NewGuid(), 200m, 0m, 20m,
            Tenders(("cash", 20m), ("Card", 150m), ("MoMo", 50m)), Day)!;

        var lines = ByKey(request);
        lines[PostingKeys.Cash].Dr.Should().Be(20m);
        lines[PostingKeys.Bank].Dr.Should().Be(200m);
    }

    [Fact]
    public void A_sale_without_tax_posts_no_vat_line()
    {
        var request = PostingRules.ForPosSale(Guid.NewGuid(), 100m, 0m, 0m, Tenders(("Cash", 100m)), Day)!;
        request.Lines.Should().NotContain(l => l.PostingKey == PostingKeys.VatOutput);
    }

    [Theory]
    [InlineData(100, 0, 10, 50)]    // tenders short of the total
    [InlineData(100, 0, 10, 120)]   // tenders over the total
    [InlineData(100, 150, 0, 0)]    // discount larger than the subtotal
    public void Inconsistent_amounts_book_nothing(int subTotal, int discount, int tax, int tendered) =>
        PostingRules.ForPosSale(Guid.NewGuid(), subTotal, discount, tax, Tenders(("Cash", tendered)), Day).Should().BeNull();

    [Fact]
    public void A_sale_without_tenders_books_nothing() =>
        PostingRules.ForPosSale(Guid.NewGuid(), 100m, 0m, 0m, Array.Empty<PosTenderLine>(), Day).Should().BeNull();

    [Fact]
    public void A_cash_shortage_debits_over_short_and_an_overage_credits_it()
    {
        var shortage = ByKey(PostingRules.ForPosShiftVariance(Guid.NewGuid(), -5m, Day)!);
        shortage[PostingKeys.CashOverShort].Should().Be((5m, 0m));
        shortage[PostingKeys.Cash].Should().Be((0m, 5m));

        var overage = ByKey(PostingRules.ForPosShiftVariance(Guid.NewGuid(), 3m, Day)!);
        overage[PostingKeys.Cash].Should().Be((3m, 0m));
        overage[PostingKeys.CashOverShort].Should().Be((0m, 3m));

        PostingRules.ForPosShiftVariance(Guid.NewGuid(), 0m, Day).Should().BeNull();
    }

    private readonly Mock<IAccountingUnitOfWork> _uow = new();
    private readonly Mock<IJournalPostingService> _posting = new();
    private readonly List<PostingRequest> _posted = new();

    private PosSaleCompletedIntegrationEventHandler SaleHandler()
    {
        _posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PostingRequest, CancellationToken>((r, _) => _posted.Add(r)).ReturnsAsync((JournalEntry?)null);
        return new(_uow.Object, _posting.Object, NullLogger<PosSaleCompletedIntegrationEventHandler>.Instance);
    }

    private static PosSaleCompletedIntegrationEvent Sale() => new()
    {
        TransactionId = Guid.NewGuid(), HasAmounts = true, SubTotal = 100m, TaxAmount = 10m, TotalAmount = 110m,
        Tenders = Tenders(("Cash", 110m)), CompletedAt = Day
    };

    [Fact]
    public async Task A_sale_is_posted_once_and_saved()
    {
        var evt = Sale();
        await SaleHandler().Handle(evt, default);

        _posted.Should().ContainSingle().Which.SourceId.Should().Be(evt.TransactionId);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_legacy_sale_without_amounts_and_an_online_paid_sale_are_skipped()
    {
        var handler = SaleHandler();
        var legacy = Sale();
        legacy.HasAmounts = false;
        var online = Sale();
        online.ExternalPaymentId = Guid.NewGuid();

        await handler.Handle(legacy, default);
        await handler.Handle(online, default);

        _posted.Should().BeEmpty();
    }

    [Fact]
    public async Task A_void_reverses_the_sale_entry()
    {
        var reversed = new List<(string Type, Guid Id)>();
        _posting.Setup(p => p.ReverseAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid, string, DateTime, CancellationToken>((t, id, _, _, _) => reversed.Add((t, id))).ReturnsAsync((JournalEntry?)null);
        var id = Guid.NewGuid();

        await new PosSaleVoidedIntegrationEventHandler(_uow.Object, _posting.Object, NullLogger<PosSaleVoidedIntegrationEventHandler>.Instance)
            .Handle(new PosSaleVoidedIntegrationEvent { TransactionId = id, Reason = "mistake", VoidedAt = Day }, default);

        reversed.Should().Equal(("PosSale", id));
    }

    [Fact]
    public async Task A_shift_without_variance_or_with_a_legacy_event_posts_nothing()
    {
        var handler = new PosShiftClosedIntegrationEventHandler(_uow.Object, _posting.Object, NullLogger<PosShiftClosedIntegrationEventHandler>.Instance);

        await handler.Handle(new PosShiftClosedIntegrationEvent { ShiftId = Guid.NewGuid(), CashVariance = 0m }, default);
        await handler.Handle(new PosShiftClosedIntegrationEvent { ShiftId = Guid.NewGuid(), CashVariance = null }, default);

        _posting.Verify(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void The_consumer_reads_the_newtonsoft_payload_pos_publishes()
    {
        var id = Guid.NewGuid();
        var json = """
        {"$type":"ERP.POS.Domain.Events.TransactionCreatedEvent, ERP.POS.Domain","TransactionId":"ID","TotalAmount":220.0,
         "CompletedAt":"2026-10-24T08:30:00Z","ExternalPaymentId":null,
         "Amounts":{"$type":"ERP.POS.Domain.Events.PosSaleAmounts, ERP.POS.Domain","SubTotal":200.0,"DiscountAmount":0.0,"TaxAmount":20.0,
           "Tenders":{"$type":"System.Collections.Generic.List`1[[ERP.POS.Domain.Events.PosTender, ERP.POS.Domain]], System.Private.CoreLib",
             "$values":[{"Method":"Cash","Amount":20.0},{"Method":"Card","Amount":200.0}]}}}
        """.Replace("ID", id.ToString());

        var sale = AccountingKafkaConsumer.ParsePosSale(JObject.Parse(json));

        sale.TransactionId.Should().Be(id);
        sale.HasAmounts.Should().BeTrue();
        sale.SubTotal.Should().Be(200m);
        sale.TaxAmount.Should().Be(20m);
        sale.Tenders.Should().Equal(new PosTenderLine("Cash", 20m), new PosTenderLine("Card", 200m));
        sale.ExternalPaymentId.Should().BeNull();

        var legacy = AccountingKafkaConsumer.ParsePosSale(JObject.Parse($$"""{"TransactionId":"{{id}}","TotalAmount":100.0}"""));
        legacy.HasAmounts.Should().BeFalse();

        var shift = AccountingKafkaConsumer.ParsePosShift(JObject.Parse($$"""{"ShiftId":"{{id}}","CashVariance":-5.0,"ClosedAt":"2026-10-24T18:00:00Z"}"""));
        shift.CashVariance.Should().Be(-5m);
        AccountingKafkaConsumer.ParsePosShift(JObject.Parse($$"""{"ShiftId":"{{id}}","CashVariance":null}""")).CashVariance.Should().BeNull();
        AccountingKafkaConsumer.ParsePosVoid(JObject.Parse($$"""{"TransactionId":"{{id}}","Reason":"x"}""")).Reason.Should().Be("x");
    }
}
