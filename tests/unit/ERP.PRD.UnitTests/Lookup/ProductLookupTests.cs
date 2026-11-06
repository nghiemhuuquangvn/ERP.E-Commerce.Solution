using ERP.PRD.Application.Features.Lookup;
using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.PRD.UnitTests.Lookup;

public class ProductLookupTests
{
    private static Product Make(string sku, ProductStatus status, Guid? uom)
    {
        var p = Product.Create("Product " + sku, sku, ProductType.Physical);
        p.Status = status;
        p.BaseUomId = uom;
        return p;
    }

    private static (GetProductLookupHandler Handler, Mock<IProductRepository> Repo) Arrange(params Product[] products)
    {
        var repo = new Mock<IProductRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) => products.Where(p => ids.Contains(p.Id)).ToList());
        var uow = new Mock<IProductUnitOfWork>();
        uow.SetupGet(u => u.Products).Returns(repo.Object);
        return (new GetProductLookupHandler(uow.Object), repo);
    }

    [Fact]
    public async Task Active_Product_Is_Reported_Usable_With_Its_Base_Unit()
    {
        var uom = Guid.NewGuid();
        var p = Make("TP-BAN01", ProductStatus.Active, uom);
        var (handler, _) = Arrange(p);

        var result = await handler.Handle(new GetProductLookupQuery { ProductIds = [p.Id] }, default);

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new ProductLookupDto { Id = p.Id, Sku = "TP-BAN01", Name = p.Name, IsActive = true, BaseUomId = uom });
    }

    [Theory]
    [InlineData(ProductStatus.Draft)]
    [InlineData(ProductStatus.Discontinued)]
    [InlineData(ProductStatus.PendingReview)]
    public async Task Product_That_Is_Not_Active_Is_Reported_Not_Usable(ProductStatus status)
    {
        var p = Make("X", status, null);
        var (handler, _) = Arrange(p);

        (await handler.Handle(new GetProductLookupQuery { ProductIds = [p.Id] }, default)).Single().IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Unknown_Ids_Are_Missing_Not_An_Error_And_Duplicates_Are_Asked_Once()
    {
        var p = Make("A", ProductStatus.Active, null);
        var (handler, repo) = Arrange(p);

        var result = await handler.Handle(new GetProductLookupQuery { ProductIds = [p.Id, p.Id, Guid.NewGuid()] }, default);

        result.Should().ContainSingle();
        repo.Verify(r => r.GetByIdsAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task No_Ids_Means_No_Query()
    {
        var (handler, repo) = Arrange();

        (await handler.Handle(new GetProductLookupQuery(), default)).Should().BeEmpty();
        repo.Verify(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
