using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Infrastructure.Data;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Queries.Barcodes;
using ERP.WH.Domain.Aggregates.Inventory;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using InventoryEntity = ERP.WH.Domain.Aggregates.Inventory.Inventory;

namespace ERP.WH.UnitTests.Caching;

/// <summary>R86: the same barcode value in two shops must resolve to each shop's own product, also from the cache.</summary>
public class BarcodeCacheTenantTests
{
    private sealed class Tenant(Guid id) : ITenantProvider
    {
        public Guid? CurrentTenantId { get; } = id;
    }

    private static readonly Guid ShopA = Guid.NewGuid();
    private static readonly Guid ShopB = Guid.NewGuid();

    /// <summary>One handler as one shop's request sees it: its own (tenant-filtered) data, the shared store behind the warehouse's cache decorator.</summary>
    private static (ResolveBarcodeQueryHandler handler, Mock<IBarcodeMappingRepository> mappings) Request(ICacheService store, Guid shop, string productName)
    {
        var productId = Guid.NewGuid();
        var mappings = new Mock<IBarcodeMappingRepository>();
        mappings.Setup(m => m.GetByBarcodeAsync("8930000000011", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BarcodeMapping("8930000000011", productId));
        var inventories = new Mock<IInventoryRepository>();
        inventories.Setup(i => i.GetByProductIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryEntity(productId, productName, 5));
        var uow = new Mock<IWarehouseUnitOfWork>();
        uow.SetupGet(u => u.BarcodeMappings).Returns(mappings.Object);
        uow.SetupGet(u => u.Inventories).Returns(inventories.Object);
        return (new ResolveBarcodeQueryHandler(uow.Object, new TenantKeyedCacheService(store, new Tenant(shop))), mappings);
    }

    private static ICacheService Store()
        => new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()), NullLogger<MemoryCacheService>.Instance);

    [Fact]
    public async Task The_same_barcode_in_two_shops_returns_each_shops_own_product_even_after_a_cache_hit()
    {
        var store = Store();
        var (shopA, mappingsA) = Request(store, ShopA, "Shop A shirt");
        var (shopB, mappingsB) = Request(store, ShopB, "Shop B hat");
        var query = new ResolveBarcodeQuery { Barcode = "8930000000011" };

        (await shopA.Handle(query, default))!.ProductName.Should().Be("Shop A shirt");
        (await shopB.Handle(query, default))!.ProductName.Should().Be("Shop B hat", "shop B must not be served shop A's cached lookup");

        // second round is answered from each shop's own entry: the databases are not asked again
        (await shopA.Handle(query, default))!.ProductName.Should().Be("Shop A shirt");
        (await shopB.Handle(query, default))!.ProductName.Should().Be("Shop B hat");
        mappingsA.Verify(m => m.GetByBarcodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        mappingsB.Verify(m => m.GetByBarcodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
