using ERP.Shared.Authorization;
using ERP.SO.Api.Controllers;
using ERP.SO.Application.Commands;
using ERP.SO.Application.DTOs;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ERP.SO.UnitTests.Orders;

/// <summary>R113: only a caller who holds sales_order:price_override may book a price other than the system price; the flag comes from the token, never from the request body.</summary>
public class PriceOverridePermissionTests
{
    private readonly Mock<IMediator> _mediator = new();
    private static readonly Guid Me = Guid.NewGuid();

    private SalesOrdersController Orders(params string[] permissions) =>
        new(_mediator.Object, new UserContext { UserId = Me.ToString(), TenantId = Guid.NewGuid().ToString(), Permissions = permissions });

    private QuotationsController Quotations(params string[] permissions) =>
        new(_mediator.Object, new UserContext { UserId = Me.ToString(), TenantId = Guid.NewGuid().ToString(), Permissions = permissions });

    private CreateOrderCommand? _order;
    private CreateQuotationCommand? _quotation;

    public PriceOverridePermissionTests()
    {
        _mediator.Setup(m => m.Send(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<SalesOrderDto>, CancellationToken>((c, _) => _order = (CreateOrderCommand)c)
            .ReturnsAsync(new SalesOrderDto { OrderId = Guid.NewGuid() });
        _mediator.Setup(m => m.Send(It.IsAny<CreateQuotationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<SalesQuotationDto>, CancellationToken>((c, _) => _quotation = (CreateQuotationCommand)c)
            .ReturnsAsync(new SalesQuotationDto { QuotationId = Guid.NewGuid() });
    }

    private static CreateOrderRequest OrderRequest() => new()
    {
        CustomerId = Me,
        LineItems = new List<OrderLineItemInput> { new() { ProductId = Guid.NewGuid(), ProductName = "Shirt", Quantity = 1, UnitPrice = 1m } },
    };

    [Fact]
    public async Task A_customer_ordering_for_themselves_never_gets_the_override()
    {
        (await Orders().CreateOrder(OrderRequest(), default)).Should().BeOfType<CreatedAtActionResult>();

        _order!.AllowPriceOverride.Should().BeFalse();
    }

    [Fact]
    public async Task Staff_with_create_permission_but_not_the_override_permission_do_not_get_it()
    {
        await Orders("sales_order:create").CreateOrder(OrderRequest(), default);

        _order!.AllowPriceOverride.Should().BeFalse();
    }

    [Fact]
    public async Task Staff_with_the_override_permission_get_it()
    {
        await Orders("sales_order:create", "sales_order:price_override").CreateOrder(OrderRequest(), default);

        _order!.AllowPriceOverride.Should().BeTrue();
    }

    [Fact]
    public async Task A_quotation_follows_the_same_rule()
    {
        var request = new CreateQuotationRequest { CustomerId = Me, LineItems = new List<QuotationLineItemInput> { new() { ProductId = Guid.NewGuid(), ProductName = "Shirt", Quantity = 1 } } };

        await Quotations("sales_order:create").CreateQuotation(request, default);
        _quotation!.AllowPriceOverride.Should().BeFalse();

        await Quotations("sales_order:create", "sales_order:price_override").CreateQuotation(request, default);
        _quotation!.AllowPriceOverride.Should().BeTrue();
    }
}
