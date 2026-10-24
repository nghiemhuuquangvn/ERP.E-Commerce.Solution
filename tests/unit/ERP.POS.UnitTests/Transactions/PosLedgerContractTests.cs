using ERP.POS.Application.Commands;
using ERP.POS.Application.Handlers;
using ERP.POS.Application.Interfaces;
using ERP.POS.Domain.Entities;
using ERP.POS.Domain.Enums;
using ERP.POS.Domain.Events;
using ERP.POS.Infrastructure.MessageBus;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.POS.UnitTests.Transactions;

/// <summary>ROADMAP P1-1c: the POS events carry what the ledger needs, and the shift figures are right.</summary>
public class PosLedgerContractTests
{
    private static Transaction Sale(Guid? shiftId = null, decimal discount = 0m, params (string Method, decimal Amount)[] tenders)
    {
        var transaction = Transaction.Open(Guid.NewGuid(), Guid.NewGuid(), shiftId);
        transaction.AddItem(Guid.NewGuid(), "Ao thun", 2, 100m, taxRate: 10m); // 200 + 20 tax
        if (discount > 0) transaction.ApplyDiscount(discount);
        if (tenders.Length == 0) tenders = new[] { ("Cash", transaction.TotalAmount) };
        transaction.CompleteWithTenders(tenders.ToList());
        return transaction;
    }

    [Fact]
    public void The_sale_event_carries_subtotal_discount_tax_and_every_tender()
    {
        var transaction = Sale(Guid.NewGuid(), 20m, ("Cash", 100m), ("Card", 100m)); // total = 200 - 20 + 20 = 200

        var created = transaction.DomainEvents.OfType<TransactionCreatedEvent>().Single();

        created.Amounts!.SubTotal.Should().Be(200m);
        created.Amounts.DiscountAmount.Should().Be(20m);
        created.Amounts.TaxAmount.Should().Be(20m);
        created.Amounts.Tenders.Should().Equal(new PosTender("Cash", 100m), new PosTender("Card", 100m));
        created.Amounts.Tenders.Sum(t => t.Amount).Should().Be(created.TotalAmount);
        created.CashShiftId.Should().Be(transaction.CashShiftId);
    }

    [Fact]
    public void The_void_event_repeats_the_amounts_so_the_sale_can_be_reversed()
    {
        var transaction = Sale();

        transaction.Void("changed mind");

        var voided = transaction.DomainEvents.OfType<TransactionVoidedEvent>().Single();
        voided.Amounts!.SubTotal.Should().Be(200m);
        voided.Amounts.Tenders.Should().ContainSingle(t => t.Method == "Cash" && t.Amount == 220m);
    }

    [Fact]
    public void Cash_tendered_counts_only_the_cash_part_of_a_split_sale()
    {
        Sale(null, 0m, ("cash", 20m), ("Card", 200m)).CashTendered.Should().Be(20m);
        Sale(null, 0m, ("Card", 220m)).CashTendered.Should().Be(0m);
    }

    [Fact]
    public void Closing_a_shift_reports_counted_expected_and_variance()
    {
        var shift = CashShift.Open(Guid.NewGuid(), Guid.NewGuid(), 500m);

        shift.Close(closingBalance: 715m, cashSalesTotal: 220m); // expected 720, 5 short

        var closed = shift.DomainEvents.OfType<CashShiftClosedEvent>().Single();
        closed.OpeningBalance.Should().Be(500m);
        closed.CountedCash.Should().Be(715m);
        closed.ExpectedCash.Should().Be(720m);
        closed.CashVariance.Should().Be(-5m);
    }

    [Fact]
    public void A_void_takes_the_sale_out_of_an_open_shift_but_not_out_of_a_closed_one()
    {
        var open = CashShift.Open(Guid.NewGuid(), Guid.NewGuid(), 0m);
        open.RecordTransaction(220m);
        open.UndoTransaction(220m);
        open.TotalSales.Should().Be(0m);
        open.TransactionCount.Should().Be(0);

        var closed = CashShift.Open(Guid.NewGuid(), Guid.NewGuid(), 0m);
        closed.RecordTransaction(220m);
        closed.Close(220m, cashSalesTotal: 220m);
        closed.UndoTransaction(220m);
        closed.TotalSales.Should().Be(220m);
    }

    [Fact]
    public async Task The_close_handler_works_out_expected_cash_from_the_completed_sales_only()
    {
        var shift = CashShift.Open(Guid.NewGuid(), Guid.NewGuid(), 100m);
        var kept = Sale(shift.Id, 0m, ("Cash", 220m));
        var voided = Sale(shift.Id, 0m, ("Cash", 220m));
        voided.Void("mistake");
        var card = Sale(shift.Id, 0m, ("Card", 220m));

        var shifts = new Mock<ICashShiftRepository>();
        shifts.Setup(s => s.GetByIdAsync(shift.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shift);
        var transactions = new Mock<ITransactionRepository>();
        transactions.Setup(t => t.GetByShiftAsync(shift.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Transaction> { kept, voided, card });
        var uow = new Mock<IPosUnitOfWork>();
        uow.SetupGet(u => u.CashShifts).Returns(shifts.Object);
        uow.SetupGet(u => u.Transactions).Returns(transactions.Object);

        await new CloseCashShiftCommandHandler(uow.Object)
            .Handle(new CloseCashShiftCommand { ShiftId = shift.Id, ClosingBalance = 320m }, default);

        var closed = shift.DomainEvents.OfType<CashShiftClosedEvent>().Single();
        closed.ExpectedCash.Should().Be(320m); // 100 float + 220 kept cash sale
        closed.CashVariance.Should().Be(0m);
    }

    [Fact]
    public void A_sale_and_its_void_share_a_kafka_key_so_they_stay_in_order()
    {
        var transaction = Sale();
        transaction.Void("x");
        var created = transaction.DomainEvents.OfType<TransactionCreatedEvent>().Single();
        var voided = transaction.DomainEvents.OfType<TransactionVoidedEvent>().Single();

        KafkaMessageBus.PartitionKey(created).Should().Be(KafkaMessageBus.PartitionKey(voided));
        KafkaMessageBus.PartitionKey(created).Should().Be(transaction.Id.ToString());
    }
}
