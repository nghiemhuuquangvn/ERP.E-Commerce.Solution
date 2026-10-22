using ERP.POS.Domain.Entities;
using ERP.POS.Domain.Events;
using FluentAssertions;
using Xunit;

namespace ERP.POS.UnitTests.Transactions;

public class TransactionEventTests
{
    private static Transaction CompletedSale(Guid productId)
    {
        var transaction = Transaction.Open(Guid.NewGuid(), Guid.NewGuid());
        transaction.AddItem(productId, "Ao thun", 2, 100m);
        transaction.Complete("Cash");
        return transaction;
    }

    [Fact]
    public void Completing_a_sale_publishes_the_sold_lines()
    {
        var productId = Guid.NewGuid();

        var created = CompletedSale(productId).DomainEvents.OfType<TransactionCreatedEvent>().Single();

        created.Lines.Should().ContainSingle(l => l.ProductId == productId && l.Quantity == 2);
    }

    [Fact]
    public void Voiding_a_completed_sale_publishes_the_returned_lines()
    {
        var productId = Guid.NewGuid();
        var transaction = CompletedSale(productId);

        transaction.Void("customer changed mind");

        transaction.DomainEvents.OfType<TransactionVoidedEvent>().Single()
            .Lines.Should().ContainSingle(l => l.ProductId == productId && l.Quantity == 2);
    }

    [Fact]
    public void Voiding_an_open_cart_moves_no_stock()
    {
        var transaction = Transaction.Open(Guid.NewGuid(), Guid.NewGuid());
        transaction.AddItem(Guid.NewGuid(), "Ao thun", 1, 100m);

        transaction.Void("abandoned");

        transaction.DomainEvents.OfType<TransactionVoidedEvent>().Should().BeEmpty();
    }
}
