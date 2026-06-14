using Xunit;
using AutoMapper;
using Moq;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.PurchaseOrders.Commands;
using ERP.PO.Application.Features.PurchaseOrders.Handlers;
using ERP.PO.Application.Mappings;

namespace ERP.PO.Application.Tests.Features.PurchaseOrders;

public class CreatePurchaseOrderCommandHandlerTests
{
    private readonly IMapper _mapper;
    private readonly CreatePurchaseOrderCommandHandler _handler;

    public CreatePurchaseOrderCommandHandlerTests()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();
        _handler = new CreatePurchaseOrderCommandHandler(_mapper);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsCreatedPurchaseOrderDto()
    {
        var command = new CreatePurchaseOrderCommand
        {
            PONumber = "PO-2024-001",
            SupplierId = Guid.NewGuid(),
            ExpectedDeliveryDate = DateTime.UtcNow.AddDays(30),
            Notes = "Urgent order",
            CreatedBy = "user@example.com"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(command.PONumber, result.PONumber);
        Assert.Equal(command.SupplierId, result.SupplierId);
    }

    [Fact]
    public async Task Handle_WithoutExpectedDeliveryDate_ReturnsCreatedPurchaseOrderDto()
    {
        var command = new CreatePurchaseOrderCommand
        {
            PONumber = "PO-2024-002",
            SupplierId = Guid.NewGuid(),
            CreatedBy = "user@example.com"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(command.PONumber, result.PONumber);
        Assert.Null(result.ExpectedDeliveryDate);
    }

    [Fact]
    public async Task Handle_SetsCreatedByProperty()
    {
        var createdBy = "manager@example.com";
        var command = new CreatePurchaseOrderCommand
        {
            PONumber = "PO-2024-003",
            SupplierId = Guid.NewGuid(),
            CreatedBy = createdBy
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Draft", result.Status);
    }
}
