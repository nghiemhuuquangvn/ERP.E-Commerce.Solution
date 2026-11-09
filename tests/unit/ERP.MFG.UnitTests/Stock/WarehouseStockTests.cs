using System.Net;
using System.Text;
using ERP.MFG.Application.Abstractions;
using ERP.MFG.Application.Features.Seeding;
using ERP.MFG.Infrastructure.Clients;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.MFG.UnitTests.Stock;

public class WarehouseStockClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls;
        public readonly List<string> Bodies = [];
        public readonly List<string> Urls = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Urls.Add(request.Method + " " + request.RequestUri!.PathAndQuery);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            return respond(request, Calls);
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode code = HttpStatusCode.OK) => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static (WarehouseStockClient Client, StubHandler Handler) Make(Func<HttpRequestMessage, int, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(t => t.CurrentTenantId).Returns(Guid.NewGuid());
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://warehouse/") };
        return (new WarehouseStockClient(http, new MemoryCache(new MemoryCacheOptions()), tenant.Object, NullLogger<WarehouseStockClient>.Instance), handler);
    }

    private const string MovementAnswer = """{"movementId":"11111111-1111-1111-1111-111111111111","movementType":"ProductionIssue","quantity":3,"recordedAt":"2026-11-10T01:00:00Z","unitCost":10,"valueDelta":-30,"warehouseId":null,"lotId":null,"replayed":false}""";

    private static StockMovementRequest Issue(string key = "MO-1#1") => new("ProductionIssue", Guid.NewGuid(), 3m, key, RequiresLot: true);

    [Fact]
    public async Task A_movement_is_posted_with_the_key_the_flags_and_camel_case_and_the_answer_is_read()
    {
        var (client, handler) = Make((_, _) => Json(MovementAnswer));

        var result = await client.RecordMovementAsync(Issue());

        result.UnitCost.Should().Be(10m);
        result.ValueDelta.Should().Be(-30m);
        result.Replayed.Should().BeFalse();
        handler.Urls.Single().Should().Be("POST /internal/v1/stock/movements");
        handler.Bodies.Single().Should().Contain("\"idempotencyKey\":\"MO-1#1\"").And.Contain("\"requiresLot\":true").And.Contain("\"movementType\":\"ProductionIssue\"");
    }

    [Fact]
    public async Task A_refusal_of_the_warehouse_keeps_its_code()
    {
        var (client, _) = Make((_, _) => Json("""{"title":"Business rule violation.","detail":"Serial-controlled items cannot be moved.","errorCode":"WH_SERIAL_NOT_SUPPORTED"}""", HttpStatusCode.BadRequest));

        var act = () => client.RecordMovementAsync(Issue());

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.Should().Match<BusinessRuleException>(e => e.RuleName == "WH_SERIAL_NOT_SUPPORTED" && e.Message.Contains("Serial"));
    }

    [Fact]
    public async Task An_idempotency_conflict_is_surfaced_at_once_and_not_retried()
    {
        var (client, handler) = Make((_, _) => Json("""{"detail":"already booked","errorCode":"WH_IDEMPOTENCY_CONFLICT"}""", HttpStatusCode.Conflict));

        var act = () => client.RecordMovementAsync(Issue());

        (await act.Should().ThrowAsync<ConcurrencyException>()).Which.ErrorCode.Should().Be("WH_IDEMPOTENCY_CONFLICT");
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task An_optimistic_locking_conflict_is_retried_and_the_second_answer_is_used()
    {
        var (client, handler) = Make((_, n) => n < 2 ? Json("""{"detail":"conflict"}""", HttpStatusCode.Conflict) : Json(MovementAnswer));

        var result = await client.RecordMovementAsync(Issue());

        result.MovementId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        handler.Calls.Should().Be(2);
    }

    [Fact]
    public async Task A_conflict_that_never_clears_is_surfaced_after_three_attempts()
    {
        var (client, handler) = Make((_, _) => Json("""{"detail":"conflict"}""", HttpStatusCode.Conflict));

        var act = () => client.RecordMovementAsync(Issue());

        (await act.Should().ThrowAsync<ConcurrencyException>()).Which.ErrorCode.Should().Be("WH_CONFLICT");
        handler.Calls.Should().Be(WarehouseStockClient.Attempts);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Any_other_failure_is_the_integration_error(HttpStatusCode code)
    {
        var (client, _) = Make((_, _) => Json("{}", code));

        var act = () => client.GetAvailabilityAsync(Guid.NewGuid(), null);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_INT_001");
    }

    [Fact]
    public async Task An_unreachable_warehouse_is_the_integration_error()
    {
        var (client, _) = Make((_, _) => throw new HttpRequestException("down"));

        var act = () => client.SuggestLotsAsync(Guid.NewGuid(), 5m, null);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("MFG_INT_001");
    }

    [Fact]
    public async Task An_unknown_reservation_is_null_and_the_query_string_carries_the_warehouse()
    {
        var (client, handler) = Make((_, _) => Json("""{"message":"No reservation for this source."}""", HttpStatusCode.NotFound));
        var warehouse = Guid.NewGuid();

        (await client.GetReservationAsync(Guid.NewGuid())).Should().BeNull();
        handler.Urls.Single().Should().StartWith("GET /internal/v1/stock/reservations/ProductionOrder/");

        var (suggest, h2) = Make((_, _) => Json("""{"productId":"11111111-1111-1111-1111-111111111111","quantity":2.5,"lots":[],"shortfall":0,"lotLessAvailable":0}"""));
        (await suggest.SuggestLotsAsync(Guid.NewGuid(), 2.5m, warehouse)).Quantity.Should().Be(2.5m);
        h2.Urls.Single().Should().Contain("quantity=2.5").And.Contain("warehouseId=" + warehouse);
    }

    [Fact]
    public async Task Nothing_is_cached_two_reads_make_two_calls()
    {
        var (client, handler) = Make((_, _) => Json("""{"productId":"11111111-1111-1111-1111-111111111111","totalQuantity":5,"reservedQuantity":0,"lockedQuantity":0,"availableQuantity":5,"unitCost":3,"balances":[]}"""));
        var id = Guid.NewGuid();

        await client.GetAvailabilityAsync(id, null);
        await client.GetAvailabilityAsync(id, null);

        handler.Calls.Should().Be(2);
    }

    [Fact]
    public async Task A_lot_status_goes_to_the_lot_route_as_a_put()
    {
        var (client, handler) = Make((_, _) => Json("""{"lotId":"11111111-1111-1111-1111-111111111111","lotNumber":"L1","status":"Blocked","unchanged":false}"""));
        var lot = Guid.NewGuid();

        var result = await client.SetLotStatusAsync(lot, "Blocked");

        result.Status.Should().Be("Blocked");
        handler.Urls.Single().Should().Be($"PUT /internal/v1/stock/lots/{lot}/status");
    }
}

public class StockCycleTests
{
    /// <summary>A small in-memory warehouse: books each key once, answers a replay, refuses another payload under the same key, tracks hold status of lots.</summary>
    private sealed class FakeStock : IWarehouseStockClient
    {
        public readonly List<StockMovementRequest> Movements = [];
        private readonly Dictionary<string, StockMovementResult> _byKey = new();
        private readonly Dictionary<string, decimal> _qtyByKey = new();
        private readonly Dictionary<Guid, string> _lotStatus = new();
        private StockReservation? _reservation;
        public string? RefuseType;

        public Task<StockAvailability> GetAvailabilityAsync(Guid productId, Guid? warehouseId, CancellationToken ct = default)
            => Task.FromResult(new StockAvailability(productId, 10, 0, 0, 10, 10m, []));

        public Task<LotSuggestion> SuggestLotsAsync(Guid productId, decimal quantity, Guid? warehouseId, CancellationToken ct = default)
        {
            var lots = _lotStatus.Where(l => l.Value == "Available").Select(l => new SuggestedLotInfo(l.Key, "FG", warehouseId, null, quantity, quantity)).ToList();
            return Task.FromResult(new LotSuggestion(productId, warehouseId, quantity, lots, 0, 0));
        }

        public Task<StockReservation> ReserveAsync(Guid productionOrderId, string? orderNumber, Guid? warehouseId, IReadOnlyList<StockReservationLine> lines, CancellationToken ct = default)
        {
            var replayed = _reservation != null;
            _reservation ??= new StockReservation(Guid.NewGuid(), "ProductionOrder", productionOrderId, orderNumber, "Reserved", false,
                lines.Select(l => new StockReservationLineResult(l.ProductId, l.SourceLineId, warehouseId, l.Quantity, 0, l.Quantity, [new StockReservationLotInfo(Guid.NewGuid(), "L1", l.Quantity, 0, l.Quantity)])).ToList());
            return Task.FromResult(_reservation with { Replayed = replayed });
        }

        public Task<StockReservation?> GetReservationAsync(Guid productionOrderId, CancellationToken ct = default)
            => Task.FromResult(_reservation == null ? null : _reservation with { Status = Movements.Count(m => m.ReservationId != null) >= 2 ? "Fulfilled" : "Reserved" });

        public Task<ReservationReleased> ReleaseReservationAsync(Guid reservationId, string? reason, CancellationToken ct = default)
            => Task.FromResult(new ReservationReleased(reservationId, "Released", DateTime.UtcNow));

        public Task<StockMovementResult> RecordMovementAsync(StockMovementRequest request, CancellationToken ct = default)
        {
            if (RefuseType == request.MovementType)
                throw new BusinessRuleException("WH_LOT_REQUIRED", "refused for the test");
            if (!request.MovementType.StartsWith("Production") && request.MovementType != "QcRelease")
                throw new BusinessRuleException("WH_MOVEMENT_TYPE", "not a production movement");
            if (!request.IdempotencyKey.Contains('#'))
                throw new BusinessRuleException("WH_REFERENCE_FORMAT", "bad key");
            if (request.RequiresLot && request.MovementType == "ProductionReceipt" && string.IsNullOrWhiteSpace(request.LotNumber))
                throw new BusinessRuleException("WH_LOT_REQUIRED", "lot number needed");
            if (_byKey.TryGetValue(request.IdempotencyKey, out var first))
            {
                if (_qtyByKey[request.IdempotencyKey] != request.Quantity && request.MovementType != "QcRelease")
                    throw new ConcurrencyException("WH_IDEMPOTENCY_CONFLICT", "different payload");
                return Task.FromResult(first with { Replayed = true });
            }

            Movements.Add(request);
            Guid? lot = request.MovementType == "ProductionReceipt" ? Guid.NewGuid() : request.LotId;
            if (request.MovementType == "ProductionReceipt") _lotStatus[lot!.Value] = request.InitialLotStatus ?? "Available";
            if (request.MovementType == "QcRelease" && request.LotId is { } released) _lotStatus[released] = "Available";
            var result = new StockMovementResult(Guid.NewGuid(), request.MovementType, request.Quantity, DateTime.UtcNow, 10m,
                request.MovementType is "ProductionIssue" or "ProductionScrap" ? -request.Quantity * 10m : request.Quantity * 10m, request.WarehouseId, lot, false);
            _byKey[request.IdempotencyKey] = result;
            _qtyByKey[request.IdempotencyKey] = request.Quantity;
            return Task.FromResult(result);
        }

        public Task<LotStatusResult> SetLotStatusAsync(Guid lotId, string status, CancellationToken ct = default)
        {
            _lotStatus[lotId] = status;
            return Task.FromResult(new LotStatusResult(lotId, "FG", status, false));
        }
    }

    private static readonly Guid Raw = Guid.NewGuid();
    private static readonly Guid Fg = Guid.NewGuid();

    private static Mock<IProductLookupClient> Products(bool lotControlled = true, bool known = true)
    {
        var products = new Mock<IProductLookupClient>();
        products.Setup(p => p.LookupAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(known ? new List<ProductInfo> { new(Raw, "RAW", "Paint", true, null, lotControlled), new(Fg, "FG", "Chair", true, null, lotControlled) } : new List<ProductInfo>());
        return products;
    }

    private static RunStockCycleCommand Command() => new(new StockCycleRequest("MO-2611-00009", Raw, Fg, Guid.NewGuid(), Guid.NewGuid()));

    [Fact]
    public async Task The_whole_cycle_plays_in_order_and_every_step_is_ok()
    {
        var stock = new FakeStock();
        var result = await new RunStockCycleHandler(stock, Products().Object).Handle(Command(), default);

        result.Ok.Should().BeTrue(string.Join("; ", result.Steps.Where(s => !s.Ok).Select(s => $"{s.Name}: {s.Detail}")));
        result.Steps.Select(s => s.Name).Should().ContainInOrder("product-flags", "availability", "suggest-lots", "reserve", "reserve-replay", "issue-1", "issue-1-replay",
            "issue-1-conflict", "issue-2", "reservation-fulfilled", "return", "scrap", "receipt-qc-hold", "hold-not-allocatable", "qc-release", "released-allocatable",
            "block-lot", "qc-release-after-block", "reverse-receipt", "refuse-legacy-type", "refuse-key-format", "refuse-lot-missing", "end-state");
    }

    [Fact]
    public async Task Every_movement_uses_the_order_number_hash_line_key_and_carries_the_item_flags()
    {
        var stock = new FakeStock();
        await new RunStockCycleHandler(stock, Products(lotControlled: true).Object).Handle(Command(), default);

        stock.Movements.Select(m => m.IdempotencyKey).Should().OnlyContain(k => k.StartsWith("MO-2611-00009#"));
        stock.Movements.Select(m => m.IdempotencyKey).Should().OnlyHaveUniqueItems();
        stock.Movements.Should().OnlyContain(m => m.RequiresLot);
        stock.Movements.Single(m => m.MovementType == "ProductionReceipt").InitialLotStatus.Should().Be("QcHold");
    }

    [Fact]
    public async Task An_unknown_product_stops_the_cycle_before_any_stock_is_touched()
    {
        var stock = new FakeStock();
        var result = await new RunStockCycleHandler(stock, Products(known: false).Object).Handle(Command(), default);

        result.Ok.Should().BeFalse();
        result.Steps.Should().ContainSingle().Which.Name.Should().Be("product-flags");
        stock.Movements.Should().BeEmpty();
    }

    [Fact]
    public async Task The_first_refusal_stops_the_cycle_and_names_the_code()
    {
        var stock = new FakeStock { RefuseType = "ProductionReturn" };
        var result = await new RunStockCycleHandler(stock, Products().Object).Handle(Command(), default);

        result.Ok.Should().BeFalse();
        var last = result.Steps.Last();
        last.Name.Should().Be("stopped");
        last.Detail.Should().StartWith("WH_LOT_REQUIRED");
        stock.Movements.Select(m => m.MovementType).Should().NotContain(["ProductionScrap", "ProductionReceipt"]);
    }
}
