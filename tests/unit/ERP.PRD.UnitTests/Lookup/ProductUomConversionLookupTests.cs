using ERP.PRD.Application.Features.Lookup;
using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.PRD.UnitTests.Lookup;

public class ProductUomConversionLookupTests
{
    [Fact]
    public async Task Conversions_Of_The_Asked_Products_Are_Returned_With_Their_Factor()
    {
        var product = Guid.NewGuid();
        var box = Guid.NewGuid();
        var piece = Guid.NewGuid();
        var conversion = new ProductUomConversion(product, box, piece, 100m);

        var repo = new Mock<IProductUomConversionRepository>();
        repo.Setup(r => r.GetByProductIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync([conversion]);
        var uow = new Mock<IProductUnitOfWork>();
        uow.SetupGet(u => u.UomConversions).Returns(repo.Object);

        var result = await new GetProductUomConversionsLookupHandler(uow.Object)
            .Handle(new GetProductUomConversionsLookupQuery { ProductIds = [product, product] }, default);

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new ProductUomConversionLookupDto { ProductId = product, FromUomId = box, ToUomId = piece, ConversionFactor = 100m });
        repo.Verify(r => r.GetByProductIdsAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task No_Ids_Means_No_Query()
    {
        var repo = new Mock<IProductUomConversionRepository>();
        var uow = new Mock<IProductUnitOfWork>();
        uow.SetupGet(u => u.UomConversions).Returns(repo.Object);

        (await new GetProductUomConversionsLookupHandler(uow.Object).Handle(new GetProductUomConversionsLookupQuery(), default)).Should().BeEmpty();
        repo.Verify(r => r.GetByProductIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
