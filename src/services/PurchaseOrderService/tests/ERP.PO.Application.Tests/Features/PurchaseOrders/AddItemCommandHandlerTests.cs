using Xunit;
using AutoMapper;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.PurchaseOrders.Commands;
using ERP.PO.Application.Features.PurchaseOrders.Handlers;
using ERP.PO.Application.Mappings;

namespace ERP.PO.Application.Tests.Features.PurchaseOrders;

public class AddItemCommandHandlerTests
{
    private readonly IMapper _mapper;
    private readonly AddItemCommandHandler _handler;

    public AddItemCommandHandlerTests()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();
        _handler = new AddItemCommandHandler(_mapper);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsUpdatedPurchaseOrder()
    {
        var command = new AddItemCommand
        {
            PurchaseOrderId = Guid.NewGuid(),
            ProductCode = "PROD-001",
            ProductName = "Product 1",
            Quantity = 10,
            Unit = "PCS",
            UnitPrice = 100.50m,
            AddedBy = "user@example.com"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.IsType<PurchaseOrderDto>(result);
    }

    [Fact]
    public async Task Handle_WithMultipleDecimalPlaces_HandlesCorrectly()
    {
        var command = new AddItemCommand
        {
            PurchaseOrderId = Guid.NewGuid(),
            ProductCode = "PROD-002",
            ProductName = "Product 2",
            Quantity = 5.5m,
            Unit = "KG",
            UnitPrice = 99.99m,
            AddedBy = "user@example.com"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_ZeroQuantity_ThrowsArgumentException()
    {
        var command = new AddItemCommand
        {
            PurchaseOrderId = Guid.NewGuid(),
            ProductCode = "PROD-003",
            ProductName = "Product 3",
            Quantity = 0,
            Unit = "PCS",
            UnitPrice = 50m,
            AddedBy = "user@example.com"
        };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _handler.Handle(command, CancellationToken.None));
    }
}
