using ERP.PRD.Application.Interfaces;
using ERP.PRD.Domain.Entities;
using ERP.PRD.Domain.Enums;
using ERP.PRD.Domain.Search;
using ERP.PRD.Infrastructure.Persistence;
using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ERP.PRD.Infrastructure.Search
{
    /// <summary>
    /// Rebuilds the Elasticsearch "products" document for a single Product from SQL — its own
    /// columns, its variants, and the resolved attribute values for both the Product itself
    /// and each of its variants (same OwnerType/OwnerId join + option-resolution logic as
    /// GetProductAttributeValuesQuery, adapted to build a document instead of a flat DTO list).
    /// </summary>
    public class ElasticsearchProductIndexer : IProductSearchIndexer
    {
        private readonly ProductDbContext _context;
        private readonly IProductPriceResolver _prices;
        private readonly ElasticsearchClient _client;
        private readonly ILogger<ElasticsearchProductIndexer> _logger;
        private readonly string _indexName;

        public ElasticsearchProductIndexer(
            ProductDbContext context,
            IProductPriceResolver prices,
            ElasticsearchClient client,
            IConfiguration configuration,
            ILogger<ElasticsearchProductIndexer> logger)
        {
            _context = context;
            _prices = prices;
            _client = client;
            _logger = logger;
            _indexName = configuration.GetValue<string>("Elasticsearch:IndexName") ?? "products";
        }

        public async Task IndexAsync(Guid productId, CancellationToken cancellationToken)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

            if (product == null || product.IsDeleted)
            {
                await _client.DeleteAsync<ProductSearchDocument>(_indexName, productId.ToString(), cancellationToken);
                _logger.LogInformation("Removed product {ProductId} from search index (not found or deleted)", productId);
                return;
            }

            string? categoryName = null;
            if (product.CategoryId.HasValue)
            {
                categoryName = await _context.Categories
                    .Where(c => c.Id == product.CategoryId.Value)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            var variants = await _context.ProductVariants
                .Where(v => v.ProductId == productId && !v.IsDeleted)
                .ToListAsync(cancellationToken);

            var variantIds = variants.Select(v => v.Id).ToList();

            // Resolve attribute values for the Product itself and each of its variants in one
            // query, exactly the same OwnerType/OwnerId shape as GetProductAttributeValuesQuery.
            var attributeValues = await _context.ProductAttributeValues
                .Where(v => !v.IsDeleted &&
                    ((v.OwnerType == AttributeOwnerType.Product && v.OwnerId == productId) ||
                     (v.OwnerType == AttributeOwnerType.ProductVariant && variantIds.Contains(v.OwnerId))))
                .ToListAsync(cancellationToken);

            var attributeIds = attributeValues.Select(v => v.ProductAttributeId).Distinct().ToList();
            var attributes = await _context.ProductAttributes
                .Include(a => a.Options)
                .Where(a => attributeIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, cancellationToken);

            var productAttributes = BuildAttributesDictionary(
                attributeValues.Where(v => v.OwnerType == AttributeOwnerType.Product && v.OwnerId == productId),
                attributes);

            // Price resolution: the same retail rule the product DTOs use (RetailPriceRules via IProductPriceResolver).
            var priceByVariant = await _prices.GetVariantPricesAsync(variantIds, cancellationToken);

            var variantDocs = new List<ProductVariantSearchDocument>();
            foreach (var variant in variants)
            {
                var variantAttrs = BuildAttributesDictionary(
                    attributeValues.Where(v => v.OwnerType == AttributeOwnerType.ProductVariant && v.OwnerId == variant.Id),
                    attributes);

                decimal? resolvedPrice = priceByVariant.TryGetValue(variant.Id, out var effectivePrice)
                    ? effectivePrice
                    : null;

                variantDocs.Add(new ProductVariantSearchDocument
                {
                    VariantId = variant.Id,
                    Sku = variant.Sku,
                    Price = resolvedPrice,
                    Length = variant.Length,
                    Width = variant.Width,
                    Height = variant.Height,
                    Attributes = variantAttrs
                });
            }

            var prices = variantDocs.Where(v => v.Price.HasValue).Select(v => v.Price!.Value).ToList();

            var document = new ProductSearchDocument
            {
                ProductId = product.Id,
                TenantId = product.TenantId,
                Sku = product.Sku,
                Name = product.Name,
                Description = product.Description,
                CategoryId = product.CategoryId,
                CategoryName = categoryName,
                Status = product.Status.ToString(),
                HasImage = product.HasImage,
                PriceMin = prices.Count > 0 ? prices.Min() : null,
                PriceMax = prices.Count > 0 ? prices.Max() : null,
                Attributes = productAttributes,
                Variants = variantDocs
            };

            await _client.IndexAsync(document, _indexName, productId.ToString(), cancellationToken);
            _logger.LogInformation("Indexed product {ProductId} into Elasticsearch index {IndexName}", productId, _indexName);
        }

        /// <summary>
        /// Resolves a set of ProductAttributeValue rows (for a single owner) into a
        /// Code -> typed-value dictionary, matching the value field/option-resolution logic in
        /// GetProductAttributeValuesQuery.
        /// </summary>
        private static Dictionary<string, object> BuildAttributesDictionary(
            IEnumerable<ProductAttributeValue> values,
            IReadOnlyDictionary<Guid, ProductAttribute> attributes)
        {
            var result = new Dictionary<string, object>();

            foreach (var value in values)
            {
                if (!attributes.TryGetValue(value.ProductAttributeId, out var attribute))
                    continue;

                object? typedValue = attribute.DataType switch
                {
                    AttributeDataType.Text => value.ValueText,
                    AttributeDataType.Number => value.ValueNumber,
                    AttributeDataType.Boolean => value.ValueBoolean,
                    AttributeDataType.Date => value.ValueDate,
                    AttributeDataType.SingleSelect => value.ProductAttributeOptionId.HasValue
                        ? attribute.Options.FirstOrDefault(o => o.Id == value.ProductAttributeOptionId.Value)?.Value
                        : null,
                    AttributeDataType.MultiSelect => ResolveMultiSelectValues(value, attribute),
                    _ => null
                };

                if (typedValue != null)
                    result[attribute.Code] = typedValue;
            }

            return result;
        }

        private static List<string>? ResolveMultiSelectValues(ProductAttributeValue value, ProductAttribute attribute)
        {
            if (string.IsNullOrWhiteSpace(value.ValueOptionIdsJson))
                return null;

            var optionIds = JsonConvert.DeserializeObject<List<Guid>>(value.ValueOptionIdsJson) ?? new List<Guid>();
            return attribute.Options
                .Where(o => optionIds.Contains(o.Id))
                .Select(o => o.Value)
                .ToList();
        }
    }
}
