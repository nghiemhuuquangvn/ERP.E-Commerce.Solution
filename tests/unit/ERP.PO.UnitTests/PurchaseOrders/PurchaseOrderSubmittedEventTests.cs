using ERP.PO.Domain.Entities;
using ERP.PO.Domain.Events;
using ERP.PO.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ERP.PO.UnitTests.PurchaseOrders;

/// <summary>Submitting a purchase order tells the approvers (Notification turns the event into a bell message for the holders of purchase_order:approve).</summary>
public class PurchaseOrderSubmittedEventTests
{
    private static PurchaseOrder Draft(decimal unitPrice = 100m, string currency = "VND")
    {
        var order = PurchaseOrder.Create("PO-9", Guid.NewGuid(), Guid.NewGuid());
        order.AddItem(PurchaseOrderItem.Create(order.Id, "SKU-1", "Item", 3, "pcs", new Money(unitPrice, currency)));
        return order;
    }

    [Fact]
    public void Submit_raises_one_event_with_the_order_number_supplier_total_and_currency()
    {
        var order = Draft(100m, "USD");
        order.ClearDomainEvents();

        order.Submit();

        var raised = order.DomainEvents.OfType<PurchaseOrderSubmittedDomainEvent>().Should().ContainSingle().Subject;
        raised.PurchaseOrderId.Should().Be(order.Id);
        raised.PONumber.Should().Be("PO-9");
        raised.SupplierId.Should().Be(order.SupplierId);
        raised.TotalAmount.Should().Be(300m);
        raised.CurrencyCode.Should().Be("USD");
    }

    [Fact]
    public void The_event_takes_part_in_the_tenant_stamping_of_every_published_event()
    {
        var order = Draft();
        order.Submit();

        order.DomainEvents.OfType<PurchaseOrderSubmittedDomainEvent>().Single().Should().BeAssignableTo<ERP.Shared.Core.Domain.ITenantEvent>();
    }

    [Fact]
    public void A_refused_submit_raises_nothing()
    {
        var order = PurchaseOrder.Create("PO-10", Guid.NewGuid(), Guid.NewGuid());
        order.ClearDomainEvents();

        var act = () => order.Submit();

        act.Should().Throw<InvalidOperationException>();
        order.DomainEvents.OfType<PurchaseOrderSubmittedDomainEvent>().Should().BeEmpty();
    }

    [Fact]
    public void Approving_does_not_raise_the_submitted_event_again()
    {
        var order = Draft();
        order.Submit();
        order.ClearDomainEvents();

        order.Approve("manager");

        order.DomainEvents.OfType<PurchaseOrderSubmittedDomainEvent>().Should().BeEmpty();
    }
}
