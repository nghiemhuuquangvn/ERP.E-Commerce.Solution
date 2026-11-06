using ERP.PRD.Application.Features.Pricing;
using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Streaming;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.PRD.Application.Features.Products.Import
{
    /// <summary>
    /// Imports products from a CSV that is still arriving (docs/architecture/streaming-import-pattern.md). The result is a stream of lines: the caller
    /// writes each one to the client as it is produced.
    /// </summary>
    public class ImportProductsStreamCommand : IStreamRequest<ImportLine>
    {
        public Stream Body { get; set; } = Stream.Null;
        public long? ContentLength { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public Guid ImportId { get; set; }
        /// <summary>Validate and look up everything, write nothing.</summary>
        public bool DryRun { get; set; }
    }

    public class ImportProductsStreamHandler : IStreamRequestHandler<ImportProductsStreamCommand, ImportLine>
    {
        private readonly IProductUnitOfWork _uow;
        private readonly PriceChangeTracker _changes;
        private readonly IImportProgressRegistry _registry;
        private readonly ImportLimits _limits;
        private readonly ILogger<ImportProductsStreamHandler> _logger;

        public ImportProductsStreamHandler(
            IProductUnitOfWork uow, PriceChangeTracker changes, IImportProgressRegistry registry,
            IOptions<ImportLimits> limits, ILogger<ImportProductsStreamHandler> logger)
        {
            _uow = uow;
            _changes = changes;
            _registry = registry;
            _limits = limits.Value;
            _logger = logger;
        }

        public async IAsyncEnumerable<ImportLine> Handle(
            ImportProductsStreamCommand request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            // Refused here (before any output) when the user already runs an import or reuses an import id: a normal 400.
            var session = _registry.BeginRun(request.TenantId, request.UserId, request.ImportId);
            var counter = new ImportBytesCounter { TotalBytes = request.ContentLength };
            var run = new ImportRun(session, counter, request.DryRun);
            var finished = false;

            try
            {
                await using var rows = CsvImportReader.ReadAsync(request.Body, ProductImportParser.RequiredColumns, _limits, counter, ct).GetAsyncEnumerator(ct);

                // The header and the first row are read BEFORE the first line is yielded, so a wrong header is still a plain 400.
                var hasRow = await rows.MoveNextAsync();
                yield return run.Started();

                var seen = new HashSet<string>(StringComparer.Ordinal);
                var batch = new List<CsvImportRow>(_limits.BatchSize);
                string? readError = null;

                while (hasRow)
                {
                    batch.Add(rows.Current);
                    try
                    {
                        hasRow = await rows.MoveNextAsync();
                    }
                    catch (BusinessRuleException ex)
                    {
                        readError = ex.Message;   // too many rows, too many bytes: the rows already read are still processed
                        hasRow = false;
                    }

                    if (batch.Count < _limits.BatchSize && hasRow) continue;

                    var outcome = await ProcessBatchAsync(batch, seen, run, ct);
                    foreach (var line in outcome.Lines) yield return line;
                    run.PublishProgress();
                    batch.Clear();

                    if (outcome.Fatal != null)
                    {
                        run.Fail(outcome.Fatal);
                        finished = true;
                        yield return new ImportErrorLine(outcome.Fatal);
                        yield break;
                    }
                }

                if (readError != null)
                {
                    run.Fail(readError);
                    finished = true;
                    yield return new ImportErrorLine(readError);
                    yield break;
                }

                finished = true;
                yield return run.Complete();
            }
            finally
            {
                if (!finished) run.Fail("The import was interrupted.");   // an exception, or the client went away
            }
        }

        private sealed record BatchOutcome(List<ImportRowLine> Lines, string? Fatal);

        private async Task<BatchOutcome> ProcessBatchAsync(List<CsvImportRow> batch, HashSet<string> seen, ImportRun run, CancellationToken ct)
        {
            var lines = new List<ImportRowLine>();
            var candidates = new List<ProductImportData>();

            foreach (var row in batch)
            {
                if (row.ParseError != null)
                {
                    lines.Add(run.Fail(row.Line, null, row.ParseError));
                    continue;
                }

                var parsed = ProductImportParser.Parse(row);
                if (parsed.Data == null)
                {
                    lines.Add(run.Fail(row.Line, parsed.Sku, parsed.Error ?? "The row is invalid."));
                    continue;
                }
                if (!seen.Add(parsed.Data.Sku))
                {
                    lines.Add(run.Skip(row.Line, parsed.Data.Sku, "The SKU repeats an earlier row of this file."));
                    continue;
                }
                candidates.Add(parsed.Data);
            }

            // One query per kind for the whole batch (not one per row).
            var existing = await _uow.Products.GetExistingSkusAsync(candidates.Select(c => c.Sku).ToList(), ct);
            var categories = await _uow.Categories.GetIdsByCodesAsync(candidates.Where(c => c.CategoryCode != null).Select(c => c.CategoryCode!).Distinct().ToList(), ct);
            var brands = await _uow.Brands.GetIdsByCodesAsync(candidates.Where(c => c.BrandCode != null).Select(c => c.BrandCode!).Distinct().ToList(), ct);

            var accepted = new List<(ProductImportData Data, Guid? CategoryId, Guid? BrandId)>();
            foreach (var candidate in candidates)
            {
                if (existing.Contains(candidate.Sku))
                {
                    lines.Add(run.Skip(candidate.Line, candidate.Sku, "A product with this SKU already exists."));
                    continue;
                }

                Guid? categoryId = null;
                if (candidate.CategoryCode != null)
                {
                    if (!categories.TryGetValue(candidate.CategoryCode, out var id))
                    {
                        lines.Add(run.Fail(candidate.Line, candidate.Sku, $"Unknown category code '{candidate.CategoryCode}'."));
                        continue;
                    }
                    categoryId = id;
                }

                Guid? brandId = null;
                if (candidate.BrandCode != null)
                {
                    if (!brands.TryGetValue(candidate.BrandCode, out var id))
                    {
                        lines.Add(run.Fail(candidate.Line, candidate.Sku, $"Unknown or inactive brand code '{candidate.BrandCode}'."));
                        continue;
                    }
                    brandId = id;
                }

                accepted.Add((candidate, categoryId, brandId));
            }

            if (accepted.Count > 0 && !run.DryRun)
            {
                try
                {
                    await WriteBatchAsync(accepted, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "A product import batch of {Count} rows could not be saved and was rolled back", accepted.Count);
                    try { await _uow.RollbackTransactionAsync(CancellationToken.None); } catch { /* nothing open */ }
                    _uow.ClearTracking();
                    foreach (var (data, _, _) in accepted)
                        lines.Add(run.Fail(data.Line, data.Sku, "Not saved: the batch was rolled back."));
                    return new BatchOutcome(lines.OrderBy(l => l.Line).ToList(), "The import stopped: a batch could not be saved. Rows reported as created before this point are saved.");
                }
            }

            foreach (var (data, _, _) in accepted)
                lines.Add(run.Ok(data.Line, data.Sku));

            return new BatchOutcome(lines.OrderBy(l => l.Line).ToList(), null);
        }

        /// <summary>One transaction per batch: the products, their default variants, the retail prices and the price-changed events are saved together.</summary>
        private async Task WriteBatchAsync(List<(ProductImportData Data, Guid? CategoryId, Guid? BrandId)> accepted, CancellationToken ct)
        {
            await _uow.BeginTransactionAsync(ct);

            var priced = new List<(Guid ProductId, Guid VariantId, decimal Price, decimal? Discounted)>();

            foreach (var (data, categoryId, brandId) in accepted)
            {
                var product = Product.Create(data.Name, data.Sku, data.ProductType, categoryId);
                if (data.Description != null) product.UpdateDescription(data.Description);
                if (brandId.HasValue) product.SetBrand(brandId);

                ProductVariant? variant = null;
                if (data.NeedsVariant)
                {
                    variant = new ProductVariant(data.Sku, data.Barcode, data.Cost ?? 0m) { ProductId = product.Id };
                    if (data.WeightKg.HasValue) variant.SetWeight(data.WeightKg.Value);
                    product.AddVariant(variant);
                }
                if (data.Active) product.Publish();

                await _uow.Products.AddAsync(product, ct);
                if (variant != null) await _uow.Variants.AddAsync(variant, ct);   // explicit: the navigation alone leaves it Modified and unstamped (R38)

                if (variant != null && data.Price.HasValue)
                    priced.Add((product.Id, variant.Id, data.Price.Value, data.DiscountedPrice));
            }

            // Step 1: the products and their variants. The price rows reference the variants by a foreign key that EF does not order for us
            // (the first live run failed with FK_PriceListItem_Variant when everything was saved at once), so they are saved in a second step.
            await _uow.SaveChangesAsync(ct);

            if (priced.Count > 0)
            {
                var list = await _uow.PriceLists.GetDefaultRetailListAsync(ct);
                var newList = list == null;
                list ??= PriceList.Create(PriceList.DefaultRetailName, DateTime.UtcNow, isActive: true, channel: PriceListChannel.Retail);
                foreach (var (_, variantId, price, discounted) in priced)
                {
                    list.AddItem(variantId, price, discounted);
                    if (!newList)
                        _uow.PriceLists.MarkItemAdded(list.Items.First(i => i.VariantId == variantId && i.MinQuantity == 1));
                }
                if (newList)
                    await _uow.PriceLists.AddAsync(list, ct);   // the whole new graph is added together

                await _uow.SaveChangesAsync(ct);

                // A new product had no price before: the "before" snapshot is empty, so each priced product raises ProductPriceChanged (POS refreshes from it).
                var before = new PriceSnapshot(priced.Select(p => p.ProductId).ToList(), new Dictionary<Guid, ResolvedProductPrice>());
                await _changes.PublishChangesAsync(before, ct);
            }

            await _uow.CommitTransactionAsync(ct);
            _uow.ClearTracking();
        }
    }
}
