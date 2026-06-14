using Xunit;
using Moq;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ERP.PO.Api.Controllers;
using ERP.PO.Application.DTOs;
using ERP.PO.Application.Features.Suppliers.Commands;
using ERP.PO.Application.Features.Suppliers.Queries;

namespace ERP.PO.Api.Tests.Controllers;

public class SuppliersControllerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<SuppliersController>> _mockLogger;
    private readonly SuppliersController _controller;

    public SuppliersControllerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<SuppliersController>>();
        _controller = new SuppliersController(_mockMediator.Object, _mockLogger.Object);
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
    public async Task CreateSupplier_WithAdminRole_ReturnsCreatedAtAction()
    {
        SetupUserContext("admin@example.com", new[] { "Admin" });

        var dto = new CreateSupplierDto
        {
            Code = "SUP-001",
            Name = "Supplier Company"
        };

        var resultDto = new SupplierDto
        {
            Id = Guid.NewGuid(),
            Code = dto.Code,
            Name = dto.Name,
            IsActive = true
        };

        _mockMediator.Setup(m => m.Send(It.IsAny<CreateSupplierCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var result = await _controller.CreateSupplier(dto);

        Assert.IsType<CreatedAtActionResult>(result);
        var createdResult = result as CreatedAtActionResult;
        Assert.Equal(nameof(_controller.GetSupplierById), createdResult!.ActionName);
    }

    [Fact]
    public async Task GetSupplierById_ValidId_ReturnsOkResult()
    {
        SetupUserContext("user@example.com");

        var supplierId = Guid.NewGuid();
        var dto = new SupplierDto
        {
            Id = supplierId,
            Code = "SUP-002",
            Name = "Supplier Company",
            IsActive = true
        };

        _mockMediator.Setup(m => m.Send(It.IsAny<GetSupplierByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _controller.GetSupplierById(supplierId);

        Assert.IsType<OkObjectResult>(result);
        var okResult = result as OkObjectResult;
        Assert.Equal(dto, okResult!.Value);
    }

    [Fact]
    public async Task GetSupplierById_NonexistentId_ReturnsNotFound()
    {
        SetupUserContext("user@example.com");

        var supplierId = Guid.NewGuid();

        _mockMediator.Setup(m => m.Send(It.IsAny<GetSupplierByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SupplierDto)null!);

        var result = await _controller.GetSupplierById(supplierId);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetSuppliers_ValidRequest_ReturnsOkResult()
    {
        SetupUserContext("user@example.com");

        var supplierList = new List<SupplierDto>
        {
            new() { Id = Guid.NewGuid(), Code = "SUP-003", Name = "Supplier 1", IsActive = true },
            new() { Id = Guid.NewGuid(), Code = "SUP-004", Name = "Supplier 2", IsActive = true }
        };

        _mockMediator.Setup(m => m.Send(It.IsAny<GetSuppliersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(supplierList);

        var result = await _controller.GetSuppliers();

        Assert.IsType<OkObjectResult>(result);
        var okResult = result as OkObjectResult;
        Assert.Equal(supplierList, okResult!.Value);
    }

    [Fact]
    public async Task UpdateSupplier_WithManagerRole_ReturnsOkResult()
    {
        SetupUserContext("manager@example.com", new[] { "Manager" });

        var supplierId = Guid.NewGuid();
        var dto = new UpdateSupplierDto
        {
            Name = "Updated Supplier Name",
            ContactPerson = "Jane Doe",
            Email = "jane@supplier.com"
        };

        var resultDto = new SupplierDto
        {
            Id = supplierId,
            Code = "SUP-005",
            Name = dto.Name,
            ContactPerson = dto.ContactPerson,
            Email = dto.Email,
            IsActive = true
        };

        _mockMediator.Setup(m => m.Send(It.IsAny<UpdateSupplierCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var result = await _controller.UpdateSupplier(supplierId, dto);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task DeactivateSupplier_WithAdminRole_ReturnsNoContent()
    {
        SetupUserContext("admin@example.com", new[] { "Admin" });

        var supplierId = Guid.NewGuid();

        _mockMediator.Setup(m => m.Send(It.IsAny<DeactivateSupplierCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.DeactivateSupplier(supplierId);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task CreateSupplier_CapturesUserIdFromClaims()
    {
        var userId = "admin@example.com";
        SetupUserContext(userId, new[] { "Admin" });

        var dto = new CreateSupplierDto
        {
            Code = "SUP-006",
            Name = "Test Supplier"
        };

        var capturedCommand = new CreateSupplierCommand();
        _mockMediator.Setup(m => m.Send(It.IsAny<CreateSupplierCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<SupplierDto>, CancellationToken>((cmd, ct) =>
            {
                capturedCommand = cmd as CreateSupplierCommand ?? capturedCommand;
            })
            .ReturnsAsync(new SupplierDto());

        await _controller.CreateSupplier(dto);

        Assert.Equal(userId, capturedCommand.CreatedBy);
    }

    [Fact]
    public async Task GetSuppliers_WithActiveFilter_PassesFilterToQuery()
    {
        SetupUserContext("user@example.com");

        _mockMediator.Setup(m => m.Send(It.IsAny<GetSuppliersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SupplierDto>());

        var capturedQuery = new GetSuppliersQuery();
        _mockMediator.Setup(m => m.Send(It.IsAny<GetSuppliersQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<IEnumerable<SupplierDto>>, CancellationToken>((qry, ct) =>
            {
                if (qry is GetSuppliersQuery gsq)
                    capturedQuery = gsq;
            })
            .ReturnsAsync(new List<SupplierDto>());

        await _controller.GetSuppliers(isActive: true, pageNumber: 2, pageSize: 20);

        Assert.True(capturedQuery.IsActive);
        Assert.Equal(2, capturedQuery.PageNumber);
        Assert.Equal(20, capturedQuery.PageSize);
    }
}
