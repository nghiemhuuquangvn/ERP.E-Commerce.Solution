using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Engineering;
using ERP.MFG.Domain.Common;
using ERP.Shared.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ERP.MFG.Infrastructure.Clients;

/// <summary>
/// Common behaviour of the clients that read other services (internal REST with a service token): any failure is MFG_INT_001 - the caller
/// never receives an empty answer that looks like "does not exist" (fail closed). Answers are cached 60 s with the tenant in the key.
/// </summary>
public abstract class InternalClientBase
{
    public static readonly TimeSpan CacheTime = TimeSpan.FromSeconds(60);
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ITenantProvider _tenant;
    private readonly ILogger _logger;
    private readonly string _service;

    protected InternalClientBase(HttpClient http, IMemoryCache cache, ITenantProvider tenant, ILogger logger, string service)
    {
        _http = http;
        _cache = cache;
        _tenant = tenant;
        _logger = logger;
        _service = service;
    }

    /// <summary>The cache key of one call: service, kind, tenant and the sorted ids. No tenant means no caching (nothing could be keyed safely).</summary>
    protected string? CacheKey(string kind, IEnumerable<Guid> ids)
        => _tenant.CurrentTenantId is { } tenant
            ? $"mfg:{_service}:{kind}:t:{tenant}:{string.Join(',', ids.OrderBy(i => i))}"
            : null;

    protected async Task<T> Cached<T>(string? key, Func<Task<T>> load) where T : class
    {
        if (key is not null && _cache.TryGetValue(key, out T? hit) && hit is not null) return hit;
        var value = await load();
        if (key is not null) _cache.Set(key, value, CacheTime);
        return value;
    }

    protected async Task<HttpResponseMessage> PostRawAsync(string path, object body, CancellationToken ct)
    {
        try
        {
            return await _http.PostAsJsonAsync(path, body, Json, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Call to {Service} {Path} failed", _service, path);
            throw Unavailable();
        }
    }

    protected async Task<T> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        using var response = await PostRawAsync(path, body, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Call to {Service} {Path} answered {Status}", _service, path, (int)response.StatusCode);
            throw Unavailable();
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, ct) ?? throw Unavailable();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Answer of {Service} {Path} could not be read", _service, path);
            throw Unavailable();
        }
    }

    protected Exception Unavailable() => MfgRules.Fail(MfgErrors.Int001, ("service", _service));
}

public sealed class ProductLookupClient(HttpClient http, IMemoryCache cache, ITenantProvider tenant, ILogger<ProductLookupClient> logger)
    : InternalClientBase(http, cache, tenant, logger, "product"), IProductLookupClient
{
    private sealed record Answer(List<ProductInfo> Items);

    public async Task<IReadOnlyList<ProductInfo>> LookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0) return [];
        return await Cached<IReadOnlyList<ProductInfo>>(CacheKey("lookup", distinct), async () =>
            (await PostAsync<Answer>("internal/v1/product-lookup", new { productIds = distinct }, ct)).Items);
    }

    public async Task<IReadOnlyList<ProductInfo>> LookupBySkuAsync(IReadOnlyCollection<string> skus, CancellationToken ct = default)
    {
        var distinct = skus.Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (distinct.Count == 0) return [];
        var result = new List<ProductInfo>();
        foreach (var chunk in distinct.Chunk(200))
            result.AddRange((await PostAsync<Answer>("internal/v1/product-lookup/by-sku", new { skus = chunk }, ct)).Items);
        return result;
    }
}

public sealed class ProductUomConversionClient(HttpClient http, IMemoryCache cache, ITenantProvider tenant, ILogger<ProductUomConversionClient> logger)
    : InternalClientBase(http, cache, tenant, logger, "product"), IProductUomConversionClient
{
    private sealed record Row(Guid ProductId, Guid FromUomId, Guid ToUomId, decimal ConversionFactor);
    private sealed record Answer(List<Row> Items);

    public async Task<IReadOnlyList<ProductConversionFacts>> LookupAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
    {
        var distinct = productIds.Distinct().ToList();
        if (distinct.Count == 0) return [];
        return await Cached<IReadOnlyList<ProductConversionFacts>>(CacheKey("uom-conversions", distinct), async () =>
            (await PostAsync<Answer>("internal/v1/product-uom-conversions/lookup", new { productIds = distinct }, ct)).Items
                .Select(r => new ProductConversionFacts(r.ProductId, r.FromUomId, r.ToUomId, r.ConversionFactor)).ToList());
    }
}

public sealed class UnitOfMeasureClient(HttpClient http, IMemoryCache cache, ITenantProvider tenant, ILogger<UnitOfMeasureClient> logger)
    : InternalClientBase(http, cache, tenant, logger, "masterdata"), IUnitOfMeasureClient
{
    private sealed record Answer(List<UnitInfo> Items);

    public async Task<IReadOnlyList<UnitInfo>> LookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0) return [];
        return await Cached<IReadOnlyList<UnitInfo>>(CacheKey("uom", distinct), async () =>
            (await PostAsync<Answer>("internal/v1/units-of-measure/lookup", new { ids = distinct }, ct)).Items);
    }

    public async Task<IReadOnlyList<UnitInfo>> LookupByCodeAsync(IReadOnlyCollection<string> codes, CancellationToken ct = default)
    {
        var distinct = codes.Select(c => c.Trim()).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (distinct.Count == 0) return [];
        var result = new List<UnitInfo>();
        foreach (var chunk in distinct.Chunk(200))
            result.AddRange((await PostAsync<Answer>("internal/v1/units-of-measure/by-code", new { codes = chunk }, ct)).Items);
        return result;
    }

    public async Task<ConvertedQuantity?> ConvertAsync(Guid fromUomId, Guid toUomId, decimal quantity, CancellationToken ct = default)
    {
        using var response = await PostRawAsync("internal/v1/units-of-measure/convert", new { fromUomId, toUomId, quantity }, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (response.StatusCode == HttpStatusCode.BadRequest) // units of different bases: a business refusal, not an outage
            throw MfgRules.Invalid("The two units of measure cannot be converted into each other (different base units).");
        if (!response.IsSuccessStatusCode) throw Unavailable();
        try
        {
            return await response.Content.ReadFromJsonAsync<ConvertedQuantity>(Json, ct) ?? throw Unavailable();
        }
        catch (JsonException)
        {
            throw Unavailable();
        }
    }
}

public sealed class EmployeeLookupClient(HttpClient http, IMemoryCache cache, ITenantProvider tenant, ILogger<EmployeeLookupClient> logger)
    : InternalClientBase(http, cache, tenant, logger, "masterdata"), IEmployeeLookupClient
{
    private sealed record Answer(List<EmployeeInfo> Items);

    public async Task<IReadOnlyList<EmployeeInfo>> LookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0) return [];
        return await Cached<IReadOnlyList<EmployeeInfo>>(CacheKey("employees", distinct), async () =>
            (await PostAsync<Answer>("internal/v1/employees/lookup", new { ids = distinct }, ct)).Items);
    }
}

public sealed class WarehouseLookupClient(HttpClient http, IMemoryCache cache, ITenantProvider tenant, ILogger<WarehouseLookupClient> logger)
    : InternalClientBase(http, cache, tenant, logger, "warehouse"), IWarehouseLookupClient
{
    private sealed record Answer(List<WarehouseInfo> Items);

    public async Task<IReadOnlyList<WarehouseInfo>> LookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0) return [];
        return await Cached<IReadOnlyList<WarehouseInfo>>(CacheKey("lookup", distinct), async () =>
            (await PostAsync<Answer>("internal/v1/warehouses/lookup", new { ids = distinct }, ct)).Items);
    }
}
