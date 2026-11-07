using ERP.PRD.Application.Features.Lookup;
using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.PRD.UnitTests.Lookup;

public class ProductLookupBySkuTests
{
    private static Product Make(string sku, ProductStatus status)
    {
        var p = Product.Create("Product " + sku, sku, ProductType.Physical);
        p.Status = status;
        p.BaseUomId = Guid.NewGuid();
        return p;
    }

    [Fact]
    public async Task Products_Are_Found_By_Sku_And_A_Shared_Sku_Returns_Both_So_The_Caller_Can_Refuse_It()
    {
        var a = Make("TP-BAN01", ProductStatus.Active);
        var b = Make("TP-BAN01", ProductStatus.Draft);
        var repo = new Mock<IProductRepository>();
        repo.Setup(r => r.GetBySkusAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([a, b]);
        var uow = new Mock<IProductUnitOfWork>();
        uow.SetupGet(u => u.Products).Returns(repo.Object);

        var result = await new GetProductLookupBySkuHandler(uow.Object).Handle(new GetProductLookupBySkuQuery { Skus = ["tp-ban01", "TP-BAN01", " "] }, default);

        result.Should().HaveCount(2);
        result.Select(r => r.IsActive).Should().BeEquivalentTo([true, false]);
        repo.Verify(r => r.GetBySkusAsync(It.Is<IReadOnlyCollection<string>>(s => s.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task No_Skus_Means_No_Query()
    {
        var repo = new Mock<IProductRepository>();
        var uow = new Mock<IProductUnitOfWork>();
        uow.SetupGet(u => u.Products).Returns(repo.Object);

        (await new GetProductLookupBySkuHandler(uow.Object).Handle(new GetProductLookupBySkuQuery(), default)).Should().BeEmpty();
        repo.Verify(r => r.GetBySkusAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
