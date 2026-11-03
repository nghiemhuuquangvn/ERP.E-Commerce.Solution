using ERP.PRD.Application.Features.Pricing.Queries;
using ERP.PRD.Application.Features.Products.Queries;
using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ERP.PRD.UnitTests.Pricing;

/// <summary>R111: the product DTOs carry the retail price (they used to carry a hard-coded 0), resolved once per page.</summary>
public class ProductQueryPriceTests
{
    private static Product NewProduct(string sku) => Product.Create("P " + sku, sku, ProductType.Physical);

    [Fact]
    public void The_mapper_puts_the_given_price_on_the_dto_and_defaults_to_zero()
    {
        var product = NewProduct("A");

        ProductTranslationMapper.MapToDto(product, null, 99m).Price.Should().Be(99m);
        ProductTranslationMapper.MapToDto(product, null).Price.Should().Be(0m);
    }

    [Fact]
    public async Task A_page_of_products_asks_for_prices_once_and_unpriced_products_stay_at_zero()
    {
        var priced = NewProduct("A");
        var unpriced = NewProduct("B");
        var resolver = new Mock<IProductPriceResolver>();
        resolver.Setup(r => r.GetProductPricesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ResolvedProductPrice> { [priced.Id] = new(25m, Guid.NewGuid()) });
        var products = new Mock<IProductRepository>();
        products.Setup(p => p.GetPagedAsync(0, 20, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Product> { priced, unpriced }, 2));
        var uow = new Mock<IProductUnitOfWork>();
        uow.SetupGet(u => u.Products).Returns(products.Object);

        var result = await new ListProductsHandler(uow.Object, resolver.Object).Handle(new ListProductsQuery(), CancellationToken.None);

        result.Products.Single(p => p.Id == priced.Id).Price.Should().Be(25m);
        result.Products.Single(p => p.Id == unpriced.Id).Price.Should().Be(0m);
        resolver.Verify(r => r.GetProductPricesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task The_search_results_carry_prices_too()
    {
        var product = NewProduct("A");
        var resolver = new Mock<IProductPriceResolver>();
        resolver.Setup(r => r.GetProductPricesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ResolvedProductPrice> { [product.Id] = new(40m, Guid.NewGuid()) });
        var products = new Mock<IProductRepository>();
        products.Setup(p => p.SearchAsync("P A", It.IsAny<CancellationToken>())).ReturnsAsync(new List<Product> { product });
        var uow = new Mock<IProductUnitOfWork>();
        uow.SetupGet(u => u.Products).Returns(products.Object);

        var result = await new SearchProductsHandler(uow.Object, resolver.Object).Handle(new SearchProductsQuery { SearchTerm = "P A" }, CancellationToken.None);

        result.Should().ContainSingle().Which.Price.Should().Be(40m);
    }

    [Fact]
    public async Task The_product_price_list_shows_the_retail_price_beside_the_cost_and_null_when_none_is_set()
    {
        var product = NewProduct("A");
        var priced = Guid.NewGuid();
        var unpriced = Guid.NewGuid();
        var resolver = new Mock<IProductPriceResolver>();
        resolver.Setup(r => r.GetVariantPricesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, decimal> { [priced] = 55m });
        var products = new Mock<IProductRepository>();
        products.Setup(p => p.ExistsAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var variants = new Mock<IProductVariantRepository>();
        variants.Setup(v => v.GetPricesByProductIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VariantPriceProjection> { new(priced, "S-1", 10m, DateTime.UtcNow), new(unpriced, "S-2", 12m, DateTime.UtcNow) });
        var uow = new Mock<IProductUnitOfWork>();
        uow.SetupGet(u => u.Products).Returns(products.Object);
        uow.SetupGet(u => u.Variants).Returns(variants.Object);

        var result = await new GetProductPricesHandler(uow.Object, resolver.Object, Mock.Of<ILogger<GetProductPricesHandler>>())
            .Handle(new GetProductPricesQuery { ProductId = product.Id }, CancellationToken.None);

        result.Single(v => v.VariantId == priced).RetailPrice.Should().Be(55m);
        result.Single(v => v.VariantId == priced).Cost.Should().Be(10m);
        result.Single(v => v.VariantId == unpriced).RetailPrice.Should().BeNull();
    }
}
