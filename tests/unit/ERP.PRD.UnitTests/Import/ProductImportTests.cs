using System.Text;
using ERP.PRD.Application.Features.Pricing;
using ERP.PRD.Application.Features.Products.Import;
using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
using ERP.PRD.Domain.Events;
using ERP.PRD.UnitTests.Pricing;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Streaming;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ERP.PRD.UnitTests.Import;

/// <summary>The streaming product import: row rules, duplicates, code lookups, batching, dry run, price events, failures.</summary>
public class ProductImportTests
{
    private const string Header = "sku,name,description,productType,categoryCode,brandCode,price,discountedPrice,cost,barcode,weightKg,status\n";

    private sealed class Harness
    {
        public readonly Mock<IProductUnitOfWork> Uow = new();
        public readonly Mock<IProductRepository> Products = new();
        public readonly Mock<ICategoryRepository> Categories = new();
        public readonly Mock<IBrandRepository> Brands = new();
        public readonly Mock<IProductVariantRepository> Variants = new();
        public readonly Mock<IPriceListRepository> PriceLists = new();
        public readonly FakeResolver Resolver = new();
        public readonly List<Product> Added = new();
        public readonly List<ProductVariant> AddedVariants = new();
        public PriceList? DefaultList;
        public HashSet<string> ExistingSkus = new();
        public Dictionary<string, Guid> CategoryIds = new();
        public Dictionary<string, Guid> BrandIds = new();
        public readonly ImportProgressRegistry Registry;
        public readonly ImportLimits Limits;

        public Harness(int batchSize = 200)
        {
            Limits = new ImportLimits { BatchSize = batchSize };
            Registry = new ImportProgressRegistry(Options.Create(Limits));

            Products.Setup(p => p.GetExistingSkusAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> skus, CancellationToken _) => new HashSet<string>(skus.Where(s => ExistingSkus.Contains(s))));
            Products.Setup(p => p.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
                .Callback<Product, CancellationToken>((p, _) => Added.Add(p)).Returns(Task.CompletedTask);
            Products.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => Added.FirstOrDefault(p => p.Id == id));
            Categories.Setup(c => c.GetIdsByCodesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> codes, CancellationToken _) => codes.Where(CategoryIds.ContainsKey).ToDictionary(c => c, c => CategoryIds[c]));
            Brands.Setup(b => b.GetIdsByCodesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> codes, CancellationToken _) => codes.Where(BrandIds.ContainsKey).ToDictionary(c => c, c => BrandIds[c]));
            Variants.Setup(v => v.AddAsync(It.IsAny<ProductVariant>(), It.IsAny<CancellationToken>()))
                .Callback<ProductVariant, CancellationToken>((v, _) => { AddedVariants.Add(v); Resolver.VariantProduct[v.Id] = v.ProductId; })
                .Returns(Task.CompletedTask);
            PriceLists.Setup(p => p.GetDefaultRetailListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => DefaultList);
            PriceLists.Setup(p => p.AddAsync(It.IsAny<PriceList>(), It.IsAny<CancellationToken>()))
                .Callback<PriceList, CancellationToken>((l, _) => { DefaultList = l; Resolver.Lists.Add(l); }).Returns(Task.CompletedTask);

            Uow.SetupGet(u => u.Products).Returns(Products.Object);
            Uow.SetupGet(u => u.Categories).Returns(Categories.Object);
            Uow.SetupGet(u => u.Brands).Returns(Brands.Object);
            Uow.SetupGet(u => u.Variants).Returns(Variants.Object);
            Uow.SetupGet(u => u.PriceLists).Returns(PriceLists.Object);
            Uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            Uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Uow.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Uow.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        }

        public ImportProductsStreamHandler Handler() =>
            new(Uow.Object, new PriceChangeTracker(Uow.Object, Resolver), Registry, Options.Create(Limits), Mock.Of<ILogger<ImportProductsStreamHandler>>());

        public ImportProductsStreamCommand Command(string csv, bool dryRun = false, Guid? importId = null) => new()
        {
            Body = new MemoryStream(Encoding.UTF8.GetBytes(csv)),
            ContentLength = Encoding.UTF8.GetByteCount(csv),
            TenantId = "tenant",
            UserId = "user",
            ImportId = importId ?? Guid.NewGuid(),
            DryRun = dryRun,
        };

        public async Task<List<ImportLine>> Run(string csv, bool dryRun = false, Guid? importId = null, CancellationToken ct = default)
        {
            var lines = new List<ImportLine>();
            await foreach (var line in Handler().Handle(Command(csv, dryRun, importId), ct)) lines.Add(line);
            return lines;
        }
    }

    private static List<ImportRowLine> Rows(IEnumerable<ImportLine> lines) => lines.OfType<ImportRowLine>().ToList();
    private static ImportSummaryLine Summary(IEnumerable<ImportLine> lines) => lines.OfType<ImportSummaryLine>().Single();

    // ── happy path ───────────────────────────────────────────────────────

    [Fact]
    public async Task Valid_rows_create_draft_products_with_category_brand_description_and_one_summary()
    {
        var h = new Harness();
        var categoryId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        h.CategoryIds["APPAREL"] = categoryId;
        h.BrandIds["ACME"] = brandId;

        var lines = await h.Run(Header + "ts-red,Red T-shirt,Cotton,Physical,apparel,acme,,,,,,\nEBOOK-1,ERP handbook,,Digital,,,,,,,,\n");

        lines.First().Should().BeOfType<ImportStartedLine>();
        Rows(lines).Select(r => (r.Line, r.Key, r.Status)).Should().Equal((2, "TS-RED", "created"), (3, "EBOOK-1", "created"));
        (Summary(lines).Processed, Summary(lines).Created, Summary(lines).Skipped, Summary(lines).Failed).Should().Be((2, 2, 0, 0));

        var red = h.Added.Single(p => p.Sku == "TS-RED");
        red.Status.Should().Be(ProductStatus.Draft);
        red.CategoryId.Should().Be(categoryId);
        red.BrandId.Should().Be(brandId);
        red.Description.Should().Be("Cotton");
        h.Added.Single(p => p.Sku == "EBOOK-1").ProductType.Should().Be(ProductType.Digital);
        h.AddedVariants.Should().BeEmpty("no price, cost, barcode or weight and not Active: no variant is needed");
    }

    [Fact]
    public async Task A_priced_row_gets_a_default_variant_a_retail_price_and_a_price_changed_event_in_one_transaction()
    {
        var h = new Harness();

        var lines = await h.Run(Header + "P-1,Priced item,,Physical,,,19.90,17.50,8.2,8930000000011,0.25,\n");

        Rows(lines).Single().Status.Should().Be("created");
        var variant = h.AddedVariants.Single();
        variant.Sku.Should().Be("P-1");
        variant.Cost.Should().Be(8.2m);
        variant.Barcode.Should().Be("8930000000011");
        variant.Weight.Should().Be(0.25m);
        var item = h.DefaultList!.Items.Single();
        (item.VariantId, item.BasePrice, item.DiscountedPrice, item.MinQuantity).Should().Be((variant.Id, 19.90m, 17.50m, 1));
        h.Added.Single().DomainEvents.OfType<ProductPriceChangedDomainEvent>().Should().ContainSingle();
        h.Uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        h.Uow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_price_goes_onto_an_existing_default_list_and_marks_the_new_item_added()
    {
        var h = new Harness();
        h.DefaultList = PriceList.Create(PriceList.DefaultRetailName, DateTime.UtcNow.AddDays(-5));
        h.Resolver.Lists.Add(h.DefaultList);

        await h.Run(Header + "P-1,Priced item,,,,,5,,,,,\n");

        h.DefaultList.Items.Should().ContainSingle();
        h.PriceLists.Verify(p => p.MarkItemAdded(It.IsAny<PriceListItem>()), Times.Once);
        h.PriceLists.Verify(p => p.AddAsync(It.IsAny<PriceList>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_active_row_gets_a_variant_and_is_published()
    {
        var h = new Harness();

        await h.Run(Header + "A-1,Active item,,,,,,,,,,Active\n");

        h.Added.Single().Status.Should().Be(ProductStatus.Active);
        h.AddedVariants.Should().ContainSingle();
    }

    // ── skipped and invalid rows ─────────────────────────────────────────

    [Fact]
    public async Task An_existing_sku_and_a_sku_repeated_in_the_file_are_skipped_with_a_reason()
    {
        var h = new Harness();
        h.ExistingSkus.Add("OLD-1");

        var lines = await h.Run(Header + "old-1,Old product,,,,,,,,,,\nNEW-1,New product,,,,,,,,,,\nnew-1,New product again,,,,,,,,,,\n");

        var rows = Rows(lines);
        rows.Select(r => r.Status).Should().Equal("skipped", "created", "skipped");
        rows[0].Message.Should().Contain("already exists");
        rows[2].Message.Should().Contain("earlier row");
        h.Added.Should().ContainSingle().Which.Sku.Should().Be("NEW-1");
        Summary(lines).Skipped.Should().Be(2);
    }

    [Theory]
    [InlineData("bad sku!,Name here", "SKU may only hold")]
    [InlineData(",Name here", "SKU is required")]
    [InlineData("S-1,ab", "name must be 3 to 255")]
    [InlineData("S-1,Name here,,Gadget", "Unknown product type")]
    [InlineData("S-1,Name here,,3", "Unknown product type")]
    [InlineData("S-1,Name here,,,,,-1", "Price")]
    [InlineData("S-1,Name here,,,,,abc", "not a number")]
    [InlineData("S-1,Name here,,,,,10,10", "less than the price")]
    [InlineData("S-1,Name here,,,,,,5", "needs a price")]
    [InlineData("S-1,Name here,,,,,,,,,,Retired", "Unknown status")]
    public async Task An_invalid_row_is_reported_with_its_line_and_nothing_is_created(string row, string reason)
    {
        var h = new Harness();

        var lines = await h.Run(Header + row + "\n");

        var line = Rows(lines).Single();
        (line.Line, line.Status).Should().Be((2, "error"));
        line.Message.Should().Contain(reason);
        h.Added.Should().BeEmpty();
        Summary(lines).Failed.Should().Be(1);
    }

    [Fact]
    public async Task A_decimal_comma_is_accepted_and_an_unknown_category_or_brand_code_fails_the_row()
    {
        var h = new Harness();

        var lines = await h.Run(Header + "C-1,Comma price,,,,,\"19,90\",,,,,\nC-2,Bad category,,,NOPE,,,,,,,\nC-3,Bad brand,,,,NOBRAND,,,,,,\n");

        Rows(lines).Select(r => r.Status).Should().Equal("created", "error", "error");
        h.DefaultList!.Items.Single().BasePrice.Should().Be(19.90m);
        Rows(lines)[1].Message.Should().Contain("category code 'NOPE'");
        Rows(lines)[2].Message.Should().Contain("brand code 'NOBRAND'");
    }

    // ── dry run, batches ─────────────────────────────────────────────────

    [Fact]
    public async Task A_dry_run_validates_and_reports_would_create_but_writes_nothing()
    {
        var h = new Harness();

        var lines = await h.Run(Header + "D-1,Dry item,,,,,5,,,,,\nbad sku,Another one\n", dryRun: true);

        Rows(lines).Select(r => r.Status).Should().Equal("would-create", "error");
        Summary(lines).DryRun.Should().BeTrue();
        h.Added.Should().BeEmpty();
        h.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        h.Uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rows_are_committed_in_batches_and_the_tracker_is_cleared_after_each()
    {
        var h = new Harness(batchSize: 2);
        var csv = Header + string.Concat(Enumerable.Range(1, 5).Select(i => $"B-{i},Batch item {i},,,,,,,,,,\n"));

        var lines = await h.Run(csv);

        Summary(lines).Created.Should().Be(5);
        h.Uow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));   // 2 + 2 + 1
        h.Uow.Verify(u => u.ClearTracking(), Times.Exactly(3));
    }

    [Fact]
    public async Task A_sku_is_looked_up_once_per_batch_not_once_per_row()
    {
        var h = new Harness(batchSize: 3);
        var csv = Header + string.Concat(Enumerable.Range(1, 6).Select(i => $"Q-{i},Query item {i},,,,,,,,,,\n"));

        await h.Run(csv);

        h.Products.Verify(p => p.GetExistingSkusAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        h.Products.Verify(p => p.SkuExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── failures and progress ────────────────────────────────────────────

    [Fact]
    public async Task A_batch_that_cannot_be_saved_is_rolled_back_reported_and_stops_the_import()
    {
        var h = new Harness(batchSize: 2);
        h.Uow.SetupSequence(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .ThrowsAsync(new Exception("db down"));
        var csv = Header + string.Concat(Enumerable.Range(1, 6).Select(i => $"F-{i},Failing item {i},,,,,,,,,,\n"));

        var lines = await h.Run(csv);

        lines.Last().Should().BeOfType<ImportErrorLine>().Which.Message.Should().NotContain("db down");
        lines.OfType<ImportSummaryLine>().Should().BeEmpty();
        var rows = Rows(lines);
        rows.Where(r => r.Status == "created").Select(r => r.Key).Should().Equal("F-1", "F-2");
        rows.Where(r => r.Status == "error").Select(r => r.Key).Should().Equal("F-3", "F-4");
        rows.Should().HaveCount(4, "the rows after the failed batch were never read");
        h.Uow.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task The_subscriber_sees_progress_then_the_final_counts()
    {
        var h = new Harness(batchSize: 2);
        var importId = Guid.NewGuid();
        var session = h.Registry.GetOrCreate("tenant", "user", importId);
        var seen = new List<ImportProgress>();
        // The first MoveNextAsync registers the subscription synchronously, so it is in place before the import starts.
        await using var subscription = session.SubscribeAsync(CancellationToken.None).GetAsyncEnumerator();
        var first = subscription.MoveNextAsync().AsTask();

        await h.Run(Header + "P-1,Item one,,,,,,,,,,\nP-2,Item two,,,,,,,,,,\nP-3,Item three,,,,,,,,,,\n", importId: importId);

        if (await first) seen.Add(subscription.Current);
        while (await subscription.MoveNextAsync()) seen.Add(subscription.Current);

        seen.Last().State.Should().Be(ImportStates.Completed);
        seen.Last().Created.Should().Be(3);
        seen.Last().Percent.Should().Be(100);
        seen.Should().Contain(p => p.State == ImportStates.Running);
    }

    [Fact]
    public async Task A_wrong_header_is_a_business_rule_error_before_any_line_and_the_session_is_failed()
    {
        var h = new Harness();
        var importId = Guid.NewGuid();

        var act = async () => await h.Run("code,title\nA,B\n", importId: importId);

        await act.Should().ThrowAsync<BusinessRuleException>();
        var states = new List<string>();
        await foreach (var p in h.Registry.GetOrCreate("tenant", "user", importId).SubscribeAsync(CancellationToken.None)) states.Add(p.State);
        states.Should().Equal(ImportStates.Failed);
    }

    [Fact]
    public async Task Too_many_rows_stops_after_processing_the_rows_already_read_with_an_error_line()
    {
        var h = new Harness(batchSize: 10);
        h.Limits.MaxRows = 2;
        var csv = Header + string.Concat(Enumerable.Range(1, 4).Select(i => $"L-{i},Limit item {i},,,,,,,,,,\n"));

        var lines = await h.Run(csv);

        Rows(lines).Select(r => r.Key).Should().Equal("L-1", "L-2");
        lines.Last().Should().BeOfType<ImportErrorLine>().Which.Message.Should().Contain("more than 2 rows");
    }

    [Fact]
    public async Task A_cancelled_import_fails_its_session_so_the_subscriber_is_not_left_waiting()
    {
        var h = new Harness();
        var importId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await h.Run(Header + "X-1,Cancelled item,,,,,,,,,,\n", importId: importId, ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        var session = h.Registry.GetOrCreate("tenant", "user", importId);
        session.IsFinished.Should().BeTrue();
    }

    [Fact]
    public async Task A_second_import_of_the_same_user_is_refused_while_one_runs_and_the_template_parses_as_valid_rows()
    {
        var h = new Harness();
        h.Registry.BeginRun("tenant", "user", Guid.NewGuid());   // another import is running

        var busy = async () => await h.Run(Header + "A-1,Item one,,,,,,,,,,\n");
        await busy.Should().ThrowAsync<BusinessRuleException>().Where(e => e.RuleName == "Import.Busy");

        var other = new Harness();
        other.CategoryIds["APPAREL"] = Guid.NewGuid();
        other.CategoryIds["BOOKS"] = Guid.NewGuid();
        other.BrandIds["ACME"] = Guid.NewGuid();
        var lines = await other.Run(ProductImportParser.TemplateCsv);
        Rows(lines).Select(r => r.Status).Should().Equal("created", "created");
    }
}
