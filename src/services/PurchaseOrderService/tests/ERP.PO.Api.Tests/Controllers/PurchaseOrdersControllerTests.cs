using Xunit;
using Moq;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ERP.PO.Api.Controllers;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.PurchaseOrders.Commands;
using ERP.PO.Application.Features.PurchaseOrders.Queries;

namespace ERP.PO.Api.Tests.Controllers;

public class PurchaseOrdersControllerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<PurchaseOrdersController>> _mockLogger;
    private readonly PurchaseOrdersController _controller;

    public PurchaseOrdersControllerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<PurchaseOrdersController>>();
        _controller = new PurchaseOrdersController(_mockMediator.Object, _mockLogger.Object);
    }

    private void SetupUserContext(string userId, string[] roles = null)
    {
        var claims = new List<Claim>
        {
            new Claim("sub", userId),
            new Claim(ClaimTypes.Name, userId)
        };

        if (roles != null)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task CreatePurchaseOrder_ValidRequest_ReturnsCreatedAtAction()
    {
        SetupUserContext("user@example.com");

        var dto = new CreatePurchaseOrderDto
        {
            PONumber = "PO-2024-001",
            SupplierId = Guid.NewGuid()
        };

        var resultDto = new PurchaseOrderDto
        {
            Id = Guid.NewGuid(),
            PONumber = dto.PONumber,
            SupplierId = dto.SupplierId,
            Status = "Draft"
        };

        _mockMediator.Setup(m => m.Send(It.IsAny<CreatePurchaseOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var result = await _controller.CreatePurchaseOrder(dto);

        Assert.IsType<CreatedAtActionResult>(result);
        var createdResult = result as CreatedAtActionResult;
        Assert.Equal(nameof(_controller.GetPurchaseOrderById), createdResult!.ActionName);
    }

    [Fact]
    public async Task GetPurchaseOrderById_ValidId_ReturnsOkResult()
    {
        SetupUserContext("user@example.com");

        var poId = Guid.NewGuid();
        var dto = new PurchaseOrderDto
        {
            Id = poId,
            PONumber = "PO-2024-002",
            Status = "Draft"
        };

        _mockMediator.Setup(m => m.Send(It.IsAny<GetPurchaseOrderByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _controller.GetPurchaseOrderById(poId);

        Assert.IsType<OkObjectResult>(result);
        var okResult = result as OkObjectResult;
        Assert.Equal(dto, okResult!.Value);
    }

    [Fact]
    public async Task GetPurchaseOrderById_NonexistentId_ReturnsNotFound()
    {
        SetupUserContext("user@example.com");

        var poId = Guid.NewGuid();

        _mockMediator.Setup(m => m.Send(It.IsAny<GetPurchaseOrderByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseOrderDto)null!);

        var result = await _controller.GetPurchaseOrderById(poId);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetPurchaseOrders_WithValidFilters_ReturnsOkResult()
    {
        SetupUserContext("user@example.com");

        var poList = new List<PurchaseOrderDto>
        {
            new() { Id = Guid.NewGuid(), PONumber = "PO-2024-003", Status = "Draft" },
            new() { Id = Guid.NewGuid(), PONumber = "PO-2024-004", Status = "Submitted" }
        };

        _mockMediator.Setup(m => m.Send(It.IsAny<GetPurchaseOrdersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(poList);

        var result = await _controller.GetPurchaseOrders();

        Assert.IsType<OkObjectResult>(result);
        var okResult = result as OkObjectResult;
        Assert.Equal(poList, okResult!.Value);
    }

    [Fact]
    public async Task SubmitPurchaseOrder_ValidId_ReturnsNoContent()
    {
        SetupUserContext("user@example.com");

        var poId = Guid.NewGuid();

        _mockMediator.Setup(m => m.Send(It.IsAny<SubmitPurchaseOrderCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.SubmitPurchaseOrder(poId);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task ApprovePurchaseOrder_WithManagerRole_ReturnsNoContent()
    {
        SetupUserContext("manager@example.com", new[] { "Manager" });

        var poId = Guid.NewGuid();

        _mockMediator.Setup(m => m.Send(It.IsAny<ApprovePurchaseOrderCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.ApprovePurchaseOrder(poId);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task CancelPurchaseOrder_WithManagerRole_ReturnsNoContent()
    {
        SetupUserContext("manager@example.com", new[] { "Manager" });

        var poId = Guid.NewGuid();

        _mockMediator.Setup(m => m.Send(It.IsAny<CancelPurchaseOrderCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.CancelPurchaseOrder(poId);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task AddItem_ValidRequest_ReturnsOkResult()
    {
        SetupUserContext("user@example.com");

        var poId = Guid.NewGuid();
        var itemDto = new AddPurchaseOrderItemDto
        {
            ProductCode = "PROD-001",
            ProductName = "Product 1",
            Quantity = 10,
            Unit = "PCS",
            UnitPrice = 100
        };

        var resultDto = new PurchaseOrderDto
        {
            Id = poId,
            PONumber = "PO-2024-005",
            Items = new List<PurchaseOrderItemDto>
            {
                new()
                {
                    ProductCode = itemDto.ProductCode,
                    ProductName = itemDto.ProductName,
                    Quantity = itemDto.Quantity
                }
            }
        };

        _mockMediator.Setup(m => m.Send(It.IsAny<AddItemCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var result = await _controller.AddItem(poId, itemDto);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task RemoveItem_ValidRequest_ReturnsOkResult()
    {
        SetupUserContext("user@example.com");

        var poId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var resultDto = new PurchaseOrderDto
        {
            Id = poId,
            PONumber = "PO-2024-006",
            Items = new List<PurchaseOrderItemDto>()
        };

        _mockMediator.Setup(m => m.Send(It.IsAny<RemoveItemCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var result = await _controller.RemoveItem(poId, itemId);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task CreatePurchaseOrder_CapturesUserIdFromClaims()
    {
        var userId = "testuser@example.com";
        SetupUserContext(userId);

        var dto = new CreatePurchaseOrderDto
        {
            PONumber = "PO-2024-007",
            SupplierId = Guid.NewGuid()
        };

        var capturedCommand = new CreatePurchaseOrderCommand();
        _mockMediator.Setup(m => m.Send(It.IsAny<CreatePurchaseOrderCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<PurchaseOrderDto>, CancellationToken>((cmd, ct) =>
            {
                capturedCommand = cmd as CreatePurchaseOrderCommand ?? capturedCommand;
            })
            .ReturnsAsync(new PurchaseOrderDto());

        await _controller.CreatePurchaseOrder(dto);

        Assert.Equal(userId, capturedCommand.CreatedBy);
    }
}
