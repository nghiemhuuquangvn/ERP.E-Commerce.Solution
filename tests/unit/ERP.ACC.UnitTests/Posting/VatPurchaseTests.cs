using ERP.ACC.Application.Commands;
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

/// <summary>ROADMAP P1-3: input VAT on bills.</summary>
public class VatPurchaseTests
{
    private static Dictionary<string, (decimal Dr, decimal Cr)> ByKey(PostingRequest r) =>
        r.Lines.GroupBy(l => l.PostingKey!).ToDictionary(g => g.Key, g => (g.Sum(l => l.Debit), g.Sum(l => l.Credit)));

    [Fact]
    public void An_approved_bill_with_VAT_debits_GRNI_net_and_VAT_input_and_credits_payables_gross()
    {
        var bill = Bill.Create("BILL-V", Guid.NewGuid(), Guid.NewGuid(), 2_200_000m, DateTime.UtcNow.AddDays(30));
        bill.ApplyVat(2_000_000m, 10m, 200_000m);

        var request = PostingRules.ForBillApproval(bill, DateTime.UtcNow);

        var lines = ByKey(request);
        lines[PostingKeys.GoodsReceivedNotInvoiced].Should().Be((2_000_000m, 0m));
        lines[PostingKeys.VatInput].Should().Be((200_000m, 0m));
        lines[PostingKeys.AccountsPayable].Should().Be((0m, 2_200_000m));
        request.Lines.Sum(l => l.Debit).Should().Be(request.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public void A_bill_without_VAT_keeps_the_two_line_entry_and_paying_it_pays_the_gross()
    {
        var bill = Bill.Create("BILL-0", Guid.NewGuid(), Guid.NewGuid(), 700m, DateTime.UtcNow.AddDays(30));

        PostingRules.ForBillApproval(bill, DateTime.UtcNow).Lines.Should().HaveCount(2);
        bill.ApplyVat(700m, 0m, 0m);
        ByKey(PostingRules.ForBillPayment(bill, DateTime.UtcNow))[PostingKeys.Bank].Should().Be((0m, 700m));
    }

    [Fact]
    public void The_bill_split_must_add_up_to_the_amount()
    {
        var bill = Bill.Create("BILL-X", Guid.NewGuid(), Guid.NewGuid(), 1100m, DateTime.UtcNow.AddDays(30));

        var act = () => bill.ApplyVat(1000m, 10m, 50m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void VAT_input_is_a_posting_key_with_a_default_account_on_both_charts()
    {
        PostingKeys.All.Should().Contain(PostingKeys.VatInput);
        PostingKeys.DefaultAccountNumbers[PostingKeys.VatInput].Should().Be(PostingKeys.DefaultAccountNumbers[PostingKeys.VatOutput]);   // 14-account chart: one tax account
        ChartTemplates.Find(ChartTemplates.Vas99)!.PostingMap[PostingKeys.VatInput].Should().Be("1331");
    }

    private readonly List<Bill> _stored = new();

    private (CreateBillCommandHandler Create, ApproveBillCommandHandler Approve, List<PostingRequest> Posted) Handlers(decimal rate, bool inclusive)
    {
        var posted = new List<PostingRequest>();
        var bills = new Mock<IBillRepository>();
        bills.Setup(r => r.AddAsync(It.IsAny<Bill>(), It.IsAny<CancellationToken>())).Callback<Bill, CancellationToken>((b, _) => _stored.Add(b)).Returns(Task.CompletedTask);
        bills.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid id, CancellationToken _) => _stored.FirstOrDefault(b => b.Id == id));
        var uow = new Mock<IAccountingUnitOfWork>();
        uow.SetupGet(u => u.Bills).Returns(bills.Object);
        var posting = new Mock<IJournalPostingService>();
        posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PostingRequest, CancellationToken>((r, _) => posted.Add(r))
            .ReturnsAsync(() => JournalEntry.Create("JE-1", Guid.NewGuid(), "x"));
        var vat = new Mock<IVatPolicy>();
        vat.Setup(v => v.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new VatSettings(rate, inclusive));
        return (new CreateBillCommandHandler(uow.Object, vat.Object),
                new ApproveBillCommandHandler(uow.Object, posting.Object, NullLogger<ApproveBillCommandHandler>.Instance), posted);
    }

    private static CreateBillCommand Command(decimal amount, decimal? rate = null) =>
        new() { BillNumber = "BILL-H", SupplierId = Guid.NewGuid(), POId = Guid.NewGuid(), Amount = amount, DueDate = DateTime.UtcNow.AddDays(30), TaxRatePercent = rate };

    [Fact]
    public async Task The_bill_takes_the_tenant_rate_and_prices_excluding_tax_add_the_VAT_on_top()
    {
        var inclusive = await Handlers(10m, true).Create.Handle(Command(1_100_000m), default);
        var exclusive = await Handlers(10m, false).Create.Handle(Command(1_000_000m), default);

        (inclusive.Amount, inclusive.NetAmount, inclusive.TaxAmount).Should().Be((1_100_000m, 1_000_000m, 100_000m));
        (exclusive.Amount, exclusive.NetAmount, exclusive.TaxAmount).Should().Be((1_100_000m, 1_000_000m, 100_000m));
    }

    [Fact]
    public async Task Approving_with_the_suppliers_rate_re_splits_the_gross_before_posting()
    {
        var (create, approve, posted) = Handlers(10m, true);
        var bill = await create.Handle(Command(1_080_000m), default);        // created at 10 %: tax 98,181.82

        await approve.Handle(new ApproveBillCommand { BillId = bill.Id, TaxRatePercent = 8m }, default);

        var lines = ByKey(posted.Single());
        lines[PostingKeys.VatInput].Should().Be((80_000m, 0m));
        lines[PostingKeys.GoodsReceivedNotInvoiced].Should().Be((1_000_000m, 0m));
        lines[PostingKeys.AccountsPayable].Should().Be((0m, 1_080_000m));
    }

    [Fact]
    public async Task A_bad_rate_is_refused_when_creating_or_approving()
    {
        var (create, approve, _) = Handlers(10m, true);
        var bill = await create.Handle(Command(110m), default);

        var badCreate = () => create.Handle(Command(100m, 120m), default);
        var badApprove = () => approve.Handle(new ApproveBillCommand { BillId = bill.Id, TaxRatePercent = -5m }, default);

        await badCreate.Should().ThrowAsync<BusinessRuleException>();
        await badApprove.Should().ThrowAsync<BusinessRuleException>();
    }
}
