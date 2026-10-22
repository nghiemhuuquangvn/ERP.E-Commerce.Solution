using AutoMapper;
using ERP.PO.Application.Features.Suppliers.Commands;
using ERP.PO.Application.Features.Suppliers.Handlers;
using ERP.PO.Application.Features.Suppliers.Queries;
using ERP.PO.Application.Mappings;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Data;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.PO.UnitTests.Suppliers;

public class SupplierHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly Mock<IRepository<Supplier>> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserContextService> _userContext = new();
    private readonly IMapper _mapper =
        new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();

    public SupplierHandlerTests()
    {
        _userContext.Setup(u => u.GetCurrentUser()).Returns(new UserContext { TenantId = TenantId.ToString(), UserId = "user-1" });
        _repository.SetupGet(r => r.UnitOfWork).Returns(_unitOfWork.Object);
    }

    private static Supplier NewSupplier(string code, string name, bool active = true)
    {
        var supplier = Supplier.Create(code, name, TenantId);
        if (!active) supplier.Deactivate();
        return supplier;
    }

    [Fact]
    public async Task GetSuppliers_filters_by_active_flag_orders_by_name_and_pages()
    {
        _repository.Setup(r => r.GetAllAsync(TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            NewSupplier("S3", "Charlie"),
            NewSupplier("S1", "Alpha"),
            NewSupplier("S2", "Bravo"),
            NewSupplier("S4", "Delta", active: false),
        });
        var handler = new GetSuppliersQueryHandler(_repository.Object, _userContext.Object, _mapper);

        var page2 = await handler.Handle(new GetSuppliersQuery { IsActive = true, PageNumber = 2, PageSize = 2 }, default);
        var inactive = await handler.Handle(new GetSuppliersQuery { IsActive = false }, default);

        page2.Select(s => s.Name).Should().Equal("Charlie");
        inactive.Select(s => s.Name).Should().Equal("Delta");
    }

    [Fact]
    public async Task GetSupplierById_returns_null_when_supplier_is_not_in_the_tenant()
    {
        var handler = new GetSupplierByIdQueryHandler(_repository.Object, _userContext.Object, _mapper);

        var result = await handler.Handle(new GetSupplierByIdQuery { SupplierId = Guid.NewGuid() }, default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSupplierById_maps_the_supplier()
    {
        var supplier = NewSupplier("S1", "Alpha");
        _repository.Setup(r => r.GetByIdAsync(supplier.Id, TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(supplier);
        var handler = new GetSupplierByIdQueryHandler(_repository.Object, _userContext.Object, _mapper);

        var result = await handler.Handle(new GetSupplierByIdQuery { SupplierId = supplier.Id }, default);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Alpha");
    }

    [Fact]
    public async Task DeactivateSupplier_deactivates_and_saves()
    {
        var supplier = NewSupplier("S1", "Alpha");
        _repository.Setup(r => r.GetByIdAsync(supplier.Id, TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(supplier);
        var handler = new DeactivateSupplierCommandHandler(_repository.Object, _userContext.Object);

        await handler.Handle(new DeactivateSupplierCommand { SupplierId = supplier.Id, DeactivatedBy = "user-1" }, default);

        supplier.IsActive.Should().BeFalse();
        supplier.UpdatedBy.Should().Be("user-1");
        _repository.Verify(r => r.Update(supplier), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivateSupplier_throws_when_supplier_is_missing()
    {
        var handler = new DeactivateSupplierCommandHandler(_repository.Object, _userContext.Object);

        var act = () => handler.Handle(new DeactivateSupplierCommand { SupplierId = Guid.NewGuid() }, default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
