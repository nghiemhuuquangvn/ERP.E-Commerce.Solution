using ERP.PO.Domain.Entities;
using ERP.PO.Domain.Events;
using ERP.PO.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ERP.PO.UnitTests.PurchaseOrders;

public class PurchaseOrderApprovalTests
{
    private static PurchaseOrder SubmittedOrder(decimal unitPrice)
    {
        var order = PurchaseOrder.Create("PO-1", Guid.NewGuid(), Guid.NewGuid());
        order.AddItem(PurchaseOrderItem.Create(order.Id, "SKU-1", "Item", 2, "pcs", new Money(unitPrice, "VND")));
        order.Submit();
        return order;
    }

    [Fact]
    public void Manager_approval_below_the_threshold_carries_the_order_total()
    {
        var order = SubmittedOrder(100m);

        order.Approve("manager");

        var approved = order.DomainEvents.OfType<PurchaseOrderApprovedDomainEvent>().Single();
        approved.TotalAmount.Should().Be(200m);
        approved.CurrencyCode.Should().Be(order.TotalAmount.Currency);
    }

    [Fact]
    public void Finance_approval_carries_the_order_total()
    {
        var order = SubmittedOrder(PurchaseOrder.FinanceApprovalThreshold);

        order.Approve("manager");
        order.DomainEvents.OfType<PurchaseOrderApprovedDomainEvent>().Should().BeEmpty();
        order.ApproveFinance("finance");

        order.DomainEvents.OfType<PurchaseOrderApprovedDomainEvent>().Single()
            .TotalAmount.Should().Be(PurchaseOrder.FinanceApprovalThreshold * 2);
    }

    [Fact]
    public void Lines_cannot_change_after_draft()
    {
        var order = SubmittedOrder(100m);
        var lineId = order.Items.Single().Id;

        var add = () => order.AddItem(PurchaseOrderItem.Create(order.Id, "SKU-2", "Other", 1, "pcs", new Money(1m, "VND")));
        var remove = () => order.RemoveItem(lineId);

        add.Should().Throw<InvalidOperationException>();
        remove.Should().Throw<InvalidOperationException>();
        order.TotalAmount.Amount.Should().Be(200m);
    }
}
