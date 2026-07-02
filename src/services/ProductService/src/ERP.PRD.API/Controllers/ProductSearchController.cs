using ERP.PRD.Application.Features.Search.Commands;
using ERP.PRD.Application.Features.Search.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.PRD.API.Controllers
{
    /// <summary>
    /// Faceted/filterable catalog search backed by the derived Elasticsearch "products" index.
    /// Routed under "api/products/search/advanced" (not the shorter "api/products/search")
    /// because ProductsController already owns "GET api/products/search" for its existing,
    /// simpler SQL LIKE name/SKU search — this is an additive, more powerful sibling, not a
    /// replacement, so the older route/handler is left untouched.
    /// </summary>
    [ApiController]
    [Authorize]
    public class ProductSearchController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ProductSearchController> _logger;

        public ProductSearchController(IMediator mediator, ILogger<ProductSearchController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        /// <summary>
        /// Search products in the Elasticsearch index with free-text, category, price, and
        /// per-attribute filters (exact match via "attr.{code}", numeric range via
        /// "attr.{code}.min"/"attr.{code}.max"), plus optional facet counts via "facets"
        /// (comma-separated attribute codes). Built manually from Request.Query since MediatR
        /// command binding can't auto-bind an unbounded dynamic key set.
        /// </summary>
        [HttpGet("api/products/search/advanced")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Search(
            [FromQuery] string? q,
            [FromQuery] Guid? categoryId,
            [FromQuery] decimal? priceMin,
            [FromQuery] decimal? priceMax,
            [FromQuery] string? facets,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = new SearchProductsQuery
            {
                Q = q,
                CategoryId = categoryId,
                PriceMin = priceMin,
                PriceMax = priceMax,
                Page = page,
                PageSize = pageSize,
                FacetCodes = string.IsNullOrWhiteSpace(facets)
                    ? null
                    : facets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            };

            (query.AttributeFilters, query.AttributeRangeFilters) = ParseAttributeFilters(Request.Query);

            _logger.LogInformation("Searching products (advanced): {Query}", q);

            var result = await _mediator.Send(query);

            return Ok(result);
        }

        /// <summary>
        /// Enqueues every non-deleted Product for a full Elasticsearch reindex. Administrative
        /// bulk action — picked up by SearchIndexProcessor's regular polling loop.
        /// </summary>
        [HttpPost("api/products/search/advanced/reindex-all")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ReindexAll()
        {
            _logger.LogInformation("Enqueuing full product search reindex");

            var enqueued = await _mediator.Send(new ReindexAllProductsCommand());

            return Ok(new { enqueued });
        }

        /// <summary>
        /// Extracts "attr.{code}" (exact match) and "attr.{code}.min"/"attr.{code}.max"
        /// (numeric range) query parameters, since these have an unbounded/dynamic key set
        /// that can't be bound via a fixed action-method parameter list.
        /// </summary>
        private static (Dictionary<string, string>? Filters, Dictionary<string, (decimal? Min, decimal? Max)>? RangeFilters)
            ParseAttributeFilters(IQueryCollection queryCollection)
        {
            Dictionary<string, string>? filters = null;
            Dictionary<string, (decimal? Min, decimal? Max)>? rangeFilters = null;

            foreach (var key in queryCollection.Keys)
            {
                if (!key.StartsWith("attr.", StringComparison.OrdinalIgnoreCase))
                    continue;

                var remainder = key["attr.".Length..];
                var value = queryCollection[key].ToString();

                if (remainder.EndsWith(".min", StringComparison.OrdinalIgnoreCase))
                {
                    var code = remainder[..^".min".Length];
                    if (decimal.TryParse(value, out var min))
                    {
                        rangeFilters ??= new Dictionary<string, (decimal?, decimal?)>();
                        var existing = rangeFilters.TryGetValue(code, out var range) ? range : (null, null);
                        rangeFilters[code] = (min, existing.Max);
                    }
                }
                else if (remainder.EndsWith(".max", StringComparison.OrdinalIgnoreCase))
                {
                    var code = remainder[..^".max".Length];
                    if (decimal.TryParse(value, out var max))
                    {
                        rangeFilters ??= new Dictionary<string, (decimal?, decimal?)>();
                        var existing = rangeFilters.TryGetValue(code, out var range) ? range : (null, null);
                        rangeFilters[code] = (existing.Min, max);
                    }
                }
                else if (!string.IsNullOrEmpty(remainder))
                {
                    filters ??= new Dictionary<string, string>();
                    filters[remainder] = value;
                }
            }

            return (filters, rangeFilters);
        }
    }
}
