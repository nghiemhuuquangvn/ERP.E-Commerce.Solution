using ERP.Shared.Authorization;
using ERP.SO.Api.Controllers;
using ERP.SO.Application.DTOs;
using ERP.SO.Application.Queries;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ERP.SO.UnitTests.Orders;

/// <summary>
/// Risk R19 (browser walk-through): the CUSTOMER role holds no permissions, so "my orders" and the order confirmation answered 403 to every
/// customer. A customer sees their own orders; another customer's order is "not found"; staff with sales_order:read still see everything.
/// </summary>
public class CustomerOrderAccessTests
{
    private readonly Mock<IMediator> _mediator = new();
    private static readonly Guid Me = Guid.NewGuid();

    private SalesOrdersController Controller(Guid userId, params string[] permissions) =>
        new(_mediator.Object, new UserContext { UserId = userId.ToString(), TenantId = Guid.NewGuid().ToString(), Permissions = permissions });

    private void OrderOf(Guid customer) =>
        _mediator.Setup(m => m.Send(It.IsAny<GetOrderQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(new SalesOrderDto { OrderId = Guid.NewGuid(), CustomerId = customer });

    [Fact]
    public async Task A_customer_without_permissions_lists_and_opens_their_own_orders()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetOrdersByCustomerQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<SalesOrderSummaryDto>());
        OrderOf(Me);
        var controller = Controller(Me);

        (await controller.GetMyOrders(default)).Should().BeOfType<OkObjectResult>();
        (await controller.GetOrderById(Guid.NewGuid(), default)).Should().BeOfType<OkObjectResult>();
        _mediator.Verify(m => m.Send(It.Is<GetOrdersByCustomerQuery>(q => q.CustomerId == Me), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Another_customers_order_is_not_found_but_staff_with_read_permission_see_it()
    {
        OrderOf(Guid.NewGuid());

        (await Controller(Me).GetOrderById(Guid.NewGuid(), default)).Should().BeOfType<NotFoundObjectResult>();
        (await Controller(Guid.NewGuid(), "sales_order:read").GetOrderById(Guid.NewGuid(), default)).Should().BeOfType<OkObjectResult>();
    }
}
