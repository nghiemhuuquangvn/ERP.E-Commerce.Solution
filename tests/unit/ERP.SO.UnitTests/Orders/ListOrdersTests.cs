using ERP.SO.Application.Handlers;
using ERP.SO.Application.Queries;
using ERP.SO.Domain.Entities;
using ERP.SO.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.SO.UnitTests.Orders;

/// <summary>The order list (Admin web, Admin mobile app): the status / customer filter is part of the paged query, and each row carries the order number.</summary>
public class ListOrdersTests
{
    private static SalesOrder Order(string no) =>
        SalesOrder.Create(Guid.NewGuid(), "An", "an@example.com", new Address("1 Le Loi", "Quan 1", "Ho Chi Minh", "700000", "Vietnam", "Phuong Ben Nghe"),
            new List<OrderLineItem> { new() { ProductId = Guid.NewGuid(), ProductName = "Shirt", Quantity = 1, UnitPrice = 100m } }, orderNo: no);

    [Fact]
    public async Task The_status_filter_and_the_page_are_given_to_the_query_not_applied_to_a_page_afterwards()
    {
        var customer = Guid.NewGuid();
        var repo = new Mock<ISalesOrderRepository>();
        repo.Setup(r => r.GetTotalCountAsync(customer, OrderStatus.Submitted, It.IsAny<CancellationToken>())).ReturnsAsync(116);
        repo.Setup(r => r.GetAllAsync(40, 20, customer, OrderStatus.Submitted, It.IsAny<CancellationToken>())).ReturnsAsync(new List<SalesOrder> { Order("SO-1") });
        repo.Setup(r => r.GetByCustomerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<SalesOrder>());

        var result = await new ListOrdersQueryHandler(repo.Object).Handle(new ListOrdersQuery { Skip = 40, Take = 20, CustomerId = customer, Status = "Submitted" }, default);

        result.Total.Should().Be(116);
        result.Items.Should().ContainSingle();
        repo.Verify(r => r.GetAllAsync(40, 20, customer, OrderStatus.Submitted, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_row_carries_the_order_number()
    {
        var repo = new Mock<ISalesOrderRepository>();
        repo.Setup(r => r.GetTotalCountAsync(null, null, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        repo.Setup(r => r.GetAllAsync(0, 20, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(new List<SalesOrder> { Order("SO-10482") });
        repo.Setup(r => r.GetByCustomerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<SalesOrder>());

        var result = await new ListOrdersQueryHandler(repo.Object).Handle(new ListOrdersQuery(), default);

        result.Items.Single().OrderNo.Should().Be("SO-10482");
    }
}
