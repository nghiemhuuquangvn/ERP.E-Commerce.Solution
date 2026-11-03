using ERP.PRD.Application.Features.Pricing;
using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
using Moq;

namespace ERP.PRD.UnitTests.Pricing;

/// <summary>Reads the in-memory price lists with the real <see cref="RetailPriceRules"/>, so a handler that changes a list changes what "the database" answers.</summary>
internal sealed class FakeResolver : IProductPriceResolver
{
    public List<PriceList> Lists { get; } = new();
    public Dictionary<Guid, Guid> VariantProduct { get; } = new();

    public Task<Dictionary<Guid, decimal>> GetVariantPricesAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var result = new Dictionary<Guid, decimal>();
        foreach (var variantId in variantIds)
        {
            var candidates = Lists.SelectMany(l => l.Items.Where(i => i.VariantId == variantId)
                .Select(i => new PriceCandidate(i.VariantId, l.Channel, l.IsActive, l.EffectiveDate, l.ExpiryDate, i.MinQuantity, i.BasePrice, i.DiscountedPrice)));
            var price = RetailPriceRules.PickVariantPrice(candidates, now);
            if (price.HasValue)
                result[variantId] = price.Value;
        }

        return Task.FromResult(result);
    }

    public async Task<Dictionary<Guid, ResolvedProductPrice>> GetProductPricesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
    {
        var variantPrices = await GetVariantPricesAsync(VariantProduct.Where(p => productIds.Contains(p.Value)).Select(p => p.Key).ToList(), ct);
        var result = new Dictionary<Guid, ResolvedProductPrice>();
        foreach (var group in variantPrices.GroupBy(p => VariantProduct[p.Key]))
        {
            var cheapest = group.OrderBy(p => p.Value).First();
            result[group.Key] = new ResolvedProductPrice(cheapest.Value, cheapest.Key);
        }

        return result;
    }
}

/// <summary>A product with one variant, a unit of work whose repositories answer from memory, and the fake resolver.</summary>
internal sealed class PriceHarness
{
    public Product Product { get; } = Product.Create("T-shirt", "TS-1", ProductType.Physical);
    public ProductVariant Variant { get; }
    public FakeResolver Resolver { get; } = new();
    public PriceList? DefaultList { get; set; }
    public Mock<IProductUnitOfWork> Uow { get; } = new();
    public Mock<IPriceListRepository> PriceLists { get; } = new();
    public Mock<IProductVariantRepository> Variants { get; } = new();
    public Mock<IProductRepository> Products { get; } = new();

    public PriceHarness(decimal cost = 40m)
    {
        Variant = new ProductVariant("TS-1-V1", cost: cost) { ProductId = Product.Id };
        Resolver.VariantProduct[Variant.Id] = Product.Id;

        Variants.Setup(v => v.GetByIdAsync(Variant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Variant);
        Products.Setup(p => p.GetByIdAsync(Product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Product);
        PriceLists.Setup(p => p.GetDefaultRetailListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => DefaultList);
        PriceLists.Setup(p => p.AddAsync(It.IsAny<PriceList>(), It.IsAny<CancellationToken>()))
            .Callback<PriceList, CancellationToken>((list, _) => { DefaultList = list; Resolver.Lists.Add(list); })
            .Returns(Task.CompletedTask);

        Uow.SetupGet(u => u.Variants).Returns(Variants.Object);
        Uow.SetupGet(u => u.PriceLists).Returns(PriceLists.Object);
        Uow.SetupGet(u => u.Products).Returns(Products.Object);
        Uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        Uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Uow.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Uow.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    public PriceChangeTracker Tracker() => new(Uow.Object, Resolver);

    /// <summary>Puts a list that already exists (tracked, with items) in front of the handler.</summary>
    public PriceList ExistingDefaultList(params (Guid VariantId, decimal Price)[] items)
    {
        var list = PriceList.Create(PriceList.DefaultRetailName, DateTime.UtcNow.AddDays(-5));
        foreach (var (variantId, price) in items)
            list.AddItem(variantId, price);
        DefaultList = list;
        Resolver.Lists.Add(list);
        return list;
    }
}
