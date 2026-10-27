using ERP.PRD.Application.Features.Variants.Commands;
using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>
/// R38: a variant created through the API was refused ("it belongs to a different tenant") because it was tracked as Modified and never stamped.
/// Runs the real handler against the real ProductDbContext (EF InMemory) with a tenant.
/// </summary>
public class ProductVariantCreationTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private readonly string _database = Guid.NewGuid().ToString();

    private ProductDbContext Db(Guid tenant) => (ProductDbContext)TenantFilterCoverageTests.Create(typeof(ProductDbContext), tenant, _database);

    private Guid SeedProduct(Guid tenant)
    {
        using var db = Db(tenant);
        var product = Product.Create("Shirt", "SHIRT-1", ERP.PRD.Domain.Enums.ProductType.Physical);
        db.Products.Add(product);
        db.SaveChanges();
        return product.Id;
    }

    private static IProductUnitOfWork UnitOfWork(ProductDbContext db)
    {
        var products = new Mock<IProductRepository>();
        products.Setup(p => p.GetByIdWithVariantsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns<Guid, CancellationToken>((id, ct) => db.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == id, ct));
        var variants = new Mock<IProductVariantRepository>();
        variants.Setup(v => v.AddAsync(It.IsAny<ProductVariant>(), It.IsAny<CancellationToken>()))
            .Returns<ProductVariant, CancellationToken>(async (v, ct) => await db.ProductVariants.AddAsync(v, ct));
        var uow = new Mock<IProductUnitOfWork>();
        uow.SetupGet(u => u.Products).Returns(products.Object);
        uow.SetupGet(u => u.Variants).Returns(variants.Object);
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(ct => db.SaveChangesAsync(ct));
        return uow.Object;
    }

    [Fact]
    public async Task A_variant_is_saved_for_the_callers_tenant_with_its_weight()
    {
        var productId = SeedProduct(TenantA);

        using (var db = Db(TenantA))
        {
            var response = await new CreateProductVariantHandler(UnitOfWork(db)).Handle(
                new CreateProductVariantCommand { ProductId = productId, Sku = "SHIRT-1-M", Cost = 50000, Weight = 0.8m, WeightUnit = "kg" }, default);
            response.Sku.Should().Be("SHIRT-1-M");
        }

        using (var a = Db(TenantA))
        {
            var variant = a.ProductVariants.Single();
            variant.TenantId.Should().Be(TenantA);
            variant.Weight.Should().Be(0.8m);
        }

        using (var b = Db(TenantB))
            b.ProductVariants.Should().BeEmpty("another tenant must not see the variant");
    }

    [Fact]
    public async Task A_second_variant_with_the_same_sku_is_still_refused()
    {
        var productId = SeedProduct(TenantA);
        var command = new CreateProductVariantCommand { ProductId = productId, Sku = "SHIRT-1-M", Cost = 1 };
        using (var db = Db(TenantA)) await new CreateProductVariantHandler(UnitOfWork(db)).Handle(command, default);

        using var again = Db(TenantA);
        var act = () => new CreateProductVariantHandler(UnitOfWork(again)).Handle(command, default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }
}
