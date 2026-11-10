using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.MFG.Application.Abstractions;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ERP.MFG.Infrastructure.Clients;

/// <summary>
/// WareHouseService <c>internal/v1/stock</c> with this service's token. Reads and writes are never cached. A Warehouse refusal (400 with
/// <c>errorCode</c>) is rethrown as a business rule carrying the WH_* code; 409 with a code (idempotency conflict) is a caller error and is not
/// retried; 409 without a code (optimistic locking) is retried up to <see cref="Attempts"/> times - the repeat of an idempotent write returns the
/// first result. Anything else unexpected is MFG_INT_001 (fail closed).
/// </summary>
public sealed class WarehouseStockClient : InternalClientBase, IWarehouseStockClient
{
    public const int Attempts = 3;

    private readonly HttpClient _http;
    private readonly ILogger _logger;

    public WarehouseStockClient(HttpClient http, IMemoryCache cache, ITenantProvider tenant, ILogger<WarehouseStockClient> logger)
        : base(http, cache, tenant, logger, "warehouse")
    {
        _http = http;
        _logger = logger;
    }

    public Task<StockAvailability> GetAvailabilityAsync(Guid productId, Guid? warehouseId, CancellationToken ct = default)
        => SendAsync<StockAvailability>(HttpMethod.Get, $"internal/v1/stock/{productId}/availability{Query(("warehouseId", warehouseId))}", null, ct)!;

    public Task<LotSuggestion> SuggestLotsAsync(Guid productId, decimal quantity, Guid? warehouseId, CancellationToken ct = default)
        => SendAsync<LotSuggestion>(HttpMethod.Get,
            $"internal/v1/stock/{productId}/suggest-lots{Query(("quantity", quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("warehouseId", warehouseId))}", null, ct)!;

    public async Task<IReadOnlyList<StockLotInfo>> ListLotsAsync(Guid productId, CancellationToken ct = default)
        => await SendAsync<List<StockLotInfo>>(HttpMethod.Get, $"internal/v1/stock/{productId}/lots", null, ct) ?? [];

    public Task<StockReservation> ReserveAsync(Guid productionOrderId, string? orderNumber, Guid? warehouseId, IReadOnlyList<StockReservationLine> lines, CancellationToken ct = default)
        => SendAsync<StockReservation>(HttpMethod.Post, "internal/v1/stock/reservations", new
        {
            sourceType = "ProductionOrder",
            sourceId = productionOrderId,
            sourceReference = orderNumber,
            warehouseId,
            lines = lines.Select(l => new { l.ProductId, l.Quantity, l.SourceLineId, l.LotId })
        }, ct)!;

    public Task<StockReservation?> GetReservationAsync(Guid productionOrderId, CancellationToken ct = default)
        => SendAsync<StockReservation>(HttpMethod.Get, $"internal/v1/stock/reservations/ProductionOrder/{productionOrderId}", null, ct, notFoundIsNull: true);

    public Task<ReservationReleased> ReleaseReservationAsync(Guid reservationId, string? reason, CancellationToken ct = default)
        => SendAsync<ReservationReleased>(HttpMethod.Post, $"internal/v1/stock/reservations/{reservationId}/release", new { reason }, ct)!;

    public Task<StockMovementResult> RecordMovementAsync(StockMovementRequest request, CancellationToken ct = default)
        => SendAsync<StockMovementResult>(HttpMethod.Post, "internal/v1/stock/movements", request, ct)!;

    public Task<LotStatusResult> SetLotStatusAsync(Guid lotId, string status, CancellationToken ct = default)
        => SendAsync<LotStatusResult>(HttpMethod.Put, $"internal/v1/stock/lots/{lotId}/status", new { status }, ct)!;

    private static string Query(params (string Name, object? Value)[] parts)
    {
        var pairs = parts.Where(p => p.Value is not null).Select(p => $"{p.Name}={Uri.EscapeDataString(Convert.ToString(p.Value, System.Globalization.CultureInfo.InvariantCulture)!)}").ToList();
        return pairs.Count == 0 ? "" : "?" + string.Join('&', pairs);
    }

    private async Task<T?> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct, bool notFoundIsNull = false) where T : class
    {
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body, options: Json);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException && !ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Call to warehouse {Path} failed", path);
                throw Unavailable();
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        return await response.Content.ReadFromJsonAsync<T>(Json, ct) ?? throw Unavailable();
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Answer of warehouse {Path} could not be read", path);
                        throw Unavailable();
                    }
                }

                var (code, detail) = await ReadProblemAsync(response, ct);

                if (response.StatusCode == HttpStatusCode.NotFound && notFoundIsNull) return null;
                if (response.StatusCode == HttpStatusCode.NotFound)
                    throw new BusinessRuleException(code ?? "WH_NOT_FOUND", detail ?? "The warehouse does not know this record.");
                if (response.StatusCode == HttpStatusCode.BadRequest)
                    throw new BusinessRuleException(code ?? "WH_REFUSED", detail ?? "The warehouse refused the request.");
                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    if (code is not null) throw new ConcurrencyException(code, detail ?? "The warehouse found a conflict.");
                    if (attempt < Attempts) continue;   // optimistic locking: the repeat of an idempotent write returns the first result
                    throw new ConcurrencyException("WH_CONFLICT", detail ?? "The stock was changed by someone else; try again.");
                }

                _logger.LogWarning("Call to warehouse {Path} answered {Status}", path, (int)response.StatusCode);
                throw Unavailable();
            }
        }
    }

    private static async Task<(string? Code, string? Detail)> ReadProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            string? Read(string name) => doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            return (Read("errorCode"), Read("detail") ?? Read("message"));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
