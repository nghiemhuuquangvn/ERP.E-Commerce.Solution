using ERP.ACC.Application.EventHandlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Infrastructure.EventBus;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.Posting;

/// <summary>ROADMAP P1-1b: Warehouse stock movements reach the ledger.</summary>
public class StockMovementPostingTests
{
    private static readonly DateTime Day = new(2026, 10, 24);

    private static (string Dr, string Cr, decimal Amount) Entry(string type, decimal delta)
    {
        var request = PostingRules.ForStockMovement(Guid.NewGuid(), type, delta, Day, "ref")!;
        request.SourceType.Should().Be("StockMovement");
        var debit = request.Lines.Single(l => l.Debit > 0);
        var credit = request.Lines.Single(l => l.Credit > 0);
        debit.Debit.Should().Be(credit.Credit);
        return (debit.PostingKey!, credit.PostingKey!, debit.Debit);
    }

    [Fact]
    public void A_receipt_debits_inventory_and_credits_grni() =>
        Entry("Received", 500m).Should().Be((PostingKeys.Inventory, PostingKeys.GoodsReceivedNotInvoiced, 500m));

    [Fact]
    public void Returned_goods_debit_inventory_and_credit_cogs_reversing_the_issue() =>
        Entry("Returned", 150m).Should().Be((PostingKeys.Inventory, PostingKeys.CostOfGoodsSold, 150m));

    [Fact]
    public void An_issue_debits_cogs_and_credits_inventory() =>
        Entry("Sold", -120m).Should().Be((PostingKeys.CostOfGoodsSold, PostingKeys.Inventory, 120m));

    [Fact]
    public void A_loss_debits_the_adjustment_key_and_a_gain_reverses_it()
    {
        Entry("Damaged", -30m).Should().Be((PostingKeys.InventoryAdjustment, PostingKeys.Inventory, 30m));
        Entry("Adjusted", -30m).Should().Be((PostingKeys.InventoryAdjustment, PostingKeys.Inventory, 30m));
        Entry("Adjusted", 45m).Should().Be((PostingKeys.Inventory, PostingKeys.InventoryAdjustment, 45m));
    }

    [Theory]
    [InlineData("Transferred", 100)]
    [InlineData("Received", 0)]
    [InlineData("Sold", 0)]
    [InlineData("Unknown", 50)]
    public void Movements_that_change_no_known_value_post_nothing(string type, int delta) =>
        PostingRules.ForStockMovement(Guid.NewGuid(), type, delta, Day, null).Should().BeNull();

    [Fact]
    public async Task The_handler_posts_only_movements_that_carry_value_and_saves()
    {
        var uow = new Mock<IAccountingUnitOfWork>();
        var posting = new Mock<IJournalPostingService>();
        PostingRequest? posted = null;
        posting.Setup(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PostingRequest, CancellationToken>((r, _) => posted = r).ReturnsAsync((JournalEntry?)null);
        var handler = new StockMovementRecordedIntegrationEventHandler(uow.Object, posting.Object,
            NullLogger<StockMovementRecordedIntegrationEventHandler>.Instance);
        var movementId = Guid.NewGuid();

        await handler.Handle(new StockMovementRecordedIntegrationEvent
        {
            MovementId = movementId, MovementType = "Received", ValueDelta = 500m, RecordedAt = Day
        }, default);
        await handler.Handle(new StockMovementRecordedIntegrationEvent { MovementId = Guid.NewGuid(), MovementType = "Transferred" }, default);

        posted!.SourceId.Should().Be(movementId);
        posting.Verify(p => p.PostAsync(It.IsAny<PostingRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void The_consumer_reads_the_warehouse_payload_and_tolerates_events_without_cost()
    {
        var id = Guid.NewGuid();
        var withCost = WarehouseRabbitMqConsumer.ParseMovement(
            $$"""{"TenantId":"{{Guid.NewGuid()}}","MovementId":"{{id}}","Quantity":10,"MovementType":"Received","Reference":"PO-1","UnitCost":50.0,"ValueDelta":500.0,"RecordedAt":"2026-10-24T08:30:00Z"}""")!;
        withCost.MovementId.Should().Be(id);
        withCost.MovementType.Should().Be("Received");
        withCost.ValueDelta.Should().Be(500m);
        withCost.Reference.Should().Be("PO-1");

        var legacy = WarehouseRabbitMqConsumer.ParseMovement(
            $$"""{"MovementId":"{{id}}","MovementType":"Sold","RecordedAt":"2026-10-24T08:30:00Z"}""")!;
        legacy.ValueDelta.Should().Be(0m);
        WarehouseRabbitMqConsumer.ParseMovement("""{"MovementType":"Sold"}""").Should().BeNull();
    }
}
