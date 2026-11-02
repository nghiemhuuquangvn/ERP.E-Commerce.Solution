using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ERP.Shared.UnitTests.Caching;

/// <summary>R84 / R86: one shop must never be served another shop's cache entry.</summary>
public class TenantKeyedCacheTests
{
    private sealed class Tenant(Guid? id) : ITenantProvider
    {
        public Guid? CurrentTenantId { get; set; } = id;
    }

    private sealed record Row(string Name);

    private static readonly Guid ShopA = Guid.NewGuid();
    private static readonly Guid ShopB = Guid.NewGuid();

    private static ICacheService NewStore()
        => new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()), NullLogger<MemoryCacheService>.Instance);

    private static TenantKeyedCacheService View(ICacheService shared, Tenant tenant, params string[] platformWide)
        => new(shared, tenant, platformWide.Length > 0 ? platformWide : null);

    [Fact]
    public async Task TwoShops_SameKey_AreServedTheirOwnValue()
    {
        var shared = NewStore();
        var a = View(shared, new Tenant(ShopA));
        var b = View(shared, new Tenant(ShopB));

        await a.SetAsync("wh:barcode:8930000000011", new Row("shop A product"));
        (await b.GetAsync<Row>("wh:barcode:8930000000011")).Should().BeNull("shop B must not read shop A's entry");

        await b.SetAsync("wh:barcode:8930000000011", new Row("shop B product"));
        (await a.GetAsync<Row>("wh:barcode:8930000000011"))!.Name.Should().Be("shop A product");
        (await b.GetAsync<Row>("wh:barcode:8930000000011"))!.Name.Should().Be("shop B product");
    }

    [Fact]
    public async Task Remove_OnlyTouchesTheCallersShop()
    {
        var shared = NewStore();
        var a = View(shared, new Tenant(ShopA));
        var b = View(shared, new Tenant(ShopB));
        await a.SetAsync("wh:inventory:p1", new Row("A"));
        await b.SetAsync("wh:inventory:p1", new Row("B"));

        await a.RemoveAsync("wh:inventory:p1");

        (await a.GetAsync<Row>("wh:inventory:p1")).Should().BeNull();
        (await b.GetAsync<Row>("wh:inventory:p1"))!.Name.Should().Be("B");
        (await a.ExistsAsync("wh:inventory:p1")).Should().BeFalse();
        (await b.ExistsAsync("wh:inventory:p1")).Should().BeTrue();
    }

    [Fact]
    public async Task NoTenant_FailsClosed_NothingIsReadOrWrittenThroughASharedKey()
    {
        var shared = NewStore();
        var anonymous = View(shared, new Tenant(null));
        var empty = View(shared, new Tenant(Guid.Empty));

        await anonymous.SetAsync("k", new Row("x"));
        await empty.SetAsync("k", new Row("y"));
        (await anonymous.GetAsync<Row>("k")).Should().BeNull();
        (await shared.GetAsync<Row>("k")).Should().BeNull("a no-tenant write must not land under the bare key");
        (await shared.GetAsync<Row>("t:00000000000000000000000000000000:k")).Should().BeNull();

        var calls = 0;
        await anonymous.GetOrSetAsync("k", () => { calls++; return Task.FromResult(new Row("z")); });
        await anonymous.GetOrSetAsync("k", () => { calls++; return Task.FromResult(new Row("z")); });
        calls.Should().Be(2, "without a tenant the factory runs every time and nothing is stored");
    }

    [Fact]
    public async Task GetOrSet_RunsTheFactoryOncePerShop()
    {
        var shared = NewStore();
        var a = View(shared, new Tenant(ShopA));
        var b = View(shared, new Tenant(ShopB));
        var calls = 0;
        Task<Row> Factory(string name) { calls++; return Task.FromResult(new Row(name)); }

        (await a.GetOrSetAsync("so:x", () => Factory("A"))).Name.Should().Be("A");
        (await a.GetOrSetAsync("so:x", () => Factory("A again"))).Name.Should().Be("A");
        (await b.GetOrSetAsync("so:x", () => Factory("B"))).Name.Should().Be("B");
        calls.Should().Be(2);
    }

    [Fact]
    public async Task PlatformWidePrefix_IsSharedByEveryShop_AndInvalidatedByTheOwner()
    {
        var shared = NewStore();
        var platform = View(shared, new Tenant(Guid.NewGuid()), "settings:");
        var shop = View(shared, new Tenant(ShopA), "settings:");

        await platform.SetAsync("settings:warehouse:inventory:system", new Row("system value"));
        (await shop.GetAsync<Row>("settings:warehouse:inventory:system"))!.Name.Should().Be("system value");

        await platform.RemoveAsync("settings:warehouse:inventory:system");
        (await shop.GetAsync<Row>("settings:warehouse:inventory:system")).Should().BeNull();
    }

    [Fact]
    public async Task TheTenantIsReadAtCallTime_SoOneScopedInstanceFollowsTenantScope()
    {
        var shared = NewStore();
        var tenant = new Tenant(ShopA);
        var cache = View(shared, tenant);
        await cache.SetAsync("k", new Row("A"));

        tenant.CurrentTenantId = ShopB;
        (await cache.GetAsync<Row>("k")).Should().BeNull();
        tenant.CurrentTenantId = ShopA;
        (await cache.GetAsync<Row>("k"))!.Name.Should().Be("A");
    }

    [Fact]
    public void AddTenantKeyedCache_ReplacesICacheService_WithAScopedTenantKeyedOne()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCacheService(new ConfigurationBuilder().Build(), "test");
        services.AddScoped<ITenantProvider>(_ => new Tenant(ShopA));
        services.AddTenantKeyedCache();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICacheService>().Should().BeOfType<TenantKeyedCacheService>();
    }

    [Fact]
    public void AddTenantKeyedCache_WithoutACacheRegistered_Throws()
    {
        var act = () => new ServiceCollection().AddTenantKeyedCache();
        act.Should().Throw<InvalidOperationException>();
    }
}
