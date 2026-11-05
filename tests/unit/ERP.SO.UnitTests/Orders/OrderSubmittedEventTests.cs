using ERP.SO.Domain.Entities;
using ERP.SO.Domain.Events;
using ERP.SO.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ERP.SO.UnitTests.Orders;

/// <summary>Submitting a sales order tells the approvers (Notification turns the event into a bell message for the holders of sales_order:approve).</summary>
public class OrderSubmittedEventTests
{
    private static SalesOrder Draft(string no = "SO-77")
    {
        return SalesOrder.Create(Guid.NewGuid(), "Binh Tran", "binh@example.com", new Address("1 Le Loi", "Quan 1", "Ho Chi Minh", "700000", "Vietnam", "Phuong Ben Nghe"),
            new List<OrderLineItem> { new() { ProductId = Guid.NewGuid(), ProductName = "Shirt", Quantity = 2, UnitPrice = 150m } }, orderNo: no);
    }

    [Fact]
    public void Submit_raises_one_event_with_the_order_number_customer_and_total()
    {
        var order = Draft();
        order.ClearDomainEvents();

        order.Submit();

        var raised = order.DomainEvents.OfType<SalesOrderSubmittedEvent>().Should().ContainSingle().Subject;
        raised.OrderId.Should().Be(order.Id);
        raised.OrderNo.Should().Be("SO-77");
        raised.CustomerId.Should().Be(order.CustomerId);
        raised.CustomerName.Should().Be("Binh Tran");
        raised.TotalAmount.Should().Be(order.TotalAmount);
    }

    [Fact]
    public void The_event_is_a_tenant_event_with_its_own_id()
    {
        var order = Draft();
        order.Submit();

        var raised = order.DomainEvents.OfType<SalesOrderSubmittedEvent>().Single();
        raised.Should().BeAssignableTo<ERP.Shared.Core.Domain.ITenantEvent>();
        raised.EventId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void A_refused_submit_raises_nothing()
    {
        var order = Draft();
        order.Submit();
        order.ClearDomainEvents();

        var act = () => order.Submit();

        act.Should().Throw<Exception>();
        order.DomainEvents.OfType<SalesOrderSubmittedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void Approving_does_not_raise_the_submitted_event_again()
    {
        var order = Draft();
        order.Submit();
        order.ClearDomainEvents();

        order.Approve();

        order.DomainEvents.OfType<SalesOrderSubmittedEvent>().Should().BeEmpty();
        order.DomainEvents.OfType<SalesOrderApprovedEvent>().Should().ContainSingle();
    }
}
