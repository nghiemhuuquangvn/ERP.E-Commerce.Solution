using ERP.PO.Application.Features.PurchaseRequests;
using ERP.PO.Application.Procurement;
using ERP.PO.Domain.Entities;
using ERP.Shared.Authorization;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.PO.UnitTests.Replenishment;

/// <summary>ROADMAP P1-9: the need (available + on order against min / max), the request lifecycle, and orders created from requests.</summary>
public class ReplenishmentTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid SupplierA = Guid.NewGuid();
    private static readonly Guid SupplierB = Guid.NewGuid();

    // ─── the need ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(9, 0, 10, 50, true, 41)]    // below min, nothing coming: order up to max
    [InlineData(10, 0, 10, 50, true, 40)]   // exactly at min counts
    [InlineData(9, 30, 10, 50, false, 0)]   // 30 already on order: covered
    [InlineData(3, 4, 10, 50, true, 43)]    // some on order, still at min: only the rest to max
    [InlineData(-2, 0, 10, 50, true, 52)]   // oversold: the negative stock is ordered too
    [InlineData(11, 0, 10, 50, false, 0)]
    public void The_need_brings_available_stock_plus_goods_on_order_up_to_the_maximum(int available, int onOrder, int min, int max, bool needed, int suggested)
    {
        var need = new ReplenishmentNeed(available, onOrder, min, max, 3);

        need.IsNeeded.Should().Be(needed);
        need.SuggestedQuantity.Should().Be(suggested);
    }

    [Fact]
    public void An_alert_from_an_older_warehouse_uses_the_reorder_levels()
    {
        var need = ReplenishmentCalculator.From(available: 40, onOrder: 0, min: 0, max: 0, safety: 0, reorderPoint: 100, reorderQuantity: 500);

        (need.Min, need.Max, need.SuggestedQuantity).Should().Be((100, 600, 560));
    }

    // ─── the request ──────────────────────────────────────────────────────

    private static PurchaseRequest OpenRequest(int suggested = 41)
    {
        var need = new ReplenishmentNeed(9, 0, 10, 10 + suggested - 1 + 0, 3);
        return PurchaseRequest.CreateFromReplenishment(Guid.NewGuid(), "SHIRT", "Shirt", need);
    }

    [Fact]
    public void A_request_is_linked_to_an_order_once_and_then_closed()
    {
        var request = OpenRequest();
        var order = Guid.NewGuid();

        request.LinkPurchaseOrder(order);

        (request.Status, request.PurchaseOrderId).Should().Be((PurchaseRequestStatus.Ordered, order));
        request.IsOpen.Should().BeFalse();
        var again = () => request.LinkPurchaseOrder(Guid.NewGuid());
        again.Should().Throw<InvalidOperationException>();
        var dismiss = () => request.Dismiss();
        dismiss.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_closed_request_cannot_be_refreshed()
    {
        var request = OpenRequest();
        request.Dismiss("not needed");

        var act = () => request.Refresh(new ReplenishmentNeed(5, 0, 10, 50, 3));

        act.Should().Throw<InvalidOperationException>();
    }

    // ─── the alert handler ────────────────────────────────────────────────

    private readonly Mock<IPurchaseRequestRepository> _requests = new();
    private readonly Mock<IProcurementStore> _store = new();
    private readonly Mock<IUserContextService> _user = new();
    private readonly List<PurchaseRequest> _added = new();

    public ReplenishmentTests()
    {
        _user.Setup(u => u.GetCurrentUser()).Returns(new UserContext { UserId = "anna", TenantId = Tenant.ToString() });
        _requests.Setup(r => r.AddAsync(It.IsAny<PurchaseRequest>(), default)).Callback<PurchaseRequest, CancellationToken>((r, _) => _added.Add(r)).Returns(Task.CompletedTask);
    }

    private CreatePurchaseRequestFromStockAlertCommandHandler AlertHandler() =>
        new(_requests.Object, NullLogger<CreatePurchaseRequestFromStockAlertCommandHandler>.Instance);

    private static CreatePurchaseRequestFromStockAlertCommand Alert(Guid? productId = null, int available = 9) => new()
    {
        ProductId = productId ?? Guid.NewGuid(), ProductName = "Shirt", ProductSku = "SHIRT",
        CurrentQuantity = available, AvailableQuantity = available, MinStock = 10, MaxStock = 50, SafetyStock = 3
    };

    [Fact]
    public async Task An_alert_creates_one_request_for_the_gap_to_the_maximum_with_the_preferred_supplier()
    {
        var alert = Alert();
        _requests.Setup(r => r.GetOnOrderQuantityAsync("SHIRT", default)).ReturnsAsync(0);
        _requests.Setup(r => r.GetPreferredSupplierItemAsync(alert.ProductId, default))
            .ReturnsAsync(SupplierItem.Create(SupplierA, Tenant, alert.ProductId, 90m, 7));

        var dto = await AlertHandler().Handle(alert, default);

        dto!.SuggestedQuantity.Should().Be(41);
        (dto.ProductCode, dto.AvailableQuantity, dto.MinStock, dto.MaxStock).Should().Be(("SHIRT", 9, 10, 50));
        (dto.PreferredSupplierId, dto.UnitPrice, dto.LeadTimeDays).Should().Be((SupplierA, 90m, 7));
        _added.Should().ContainSingle();
    }

    [Fact]
    public async Task Goods_already_on_order_reduce_the_quantity_and_can_cancel_the_need()
    {
        var alert = Alert();
        _requests.Setup(r => r.GetOnOrderQuantityAsync("SHIRT", default)).ReturnsAsync(30);

        var covered = await AlertHandler().Handle(alert, default);

        covered.Should().BeNull();
        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task A_second_alert_updates_the_open_request_instead_of_adding_one()
    {
        var alert = Alert(available: 9);
        var open = PurchaseRequest.CreateFromReplenishment(alert.ProductId, "SHIRT", "Shirt", new ReplenishmentNeed(9, 0, 10, 50, 3));
        _requests.Setup(r => r.GetActiveByProductIdAsync(alert.ProductId, default)).ReturnsAsync(open);
        _requests.Setup(r => r.GetOnOrderQuantityAsync("SHIRT", default)).ReturnsAsync(0);
        alert.AvailableQuantity = 4;

        var dto = await AlertHandler().Handle(alert, default);

        dto!.Id.Should().Be(open.Id);
        dto.SuggestedQuantity.Should().Be(46);
        _added.Should().BeEmpty();
    }

    [Fact]
    public async Task When_orders_now_cover_the_need_the_open_request_is_dismissed_with_a_reason()
    {
        var alert = Alert(available: 9);
        var open = PurchaseRequest.CreateFromReplenishment(alert.ProductId, "SHIRT", "Shirt", new ReplenishmentNeed(9, 0, 10, 50, 3));
        _requests.Setup(r => r.GetActiveByProductIdAsync(alert.ProductId, default)).ReturnsAsync(open);
        _requests.Setup(r => r.GetOnOrderQuantityAsync("SHIRT", default)).ReturnsAsync(41);

        var dto = await AlertHandler().Handle(alert, default);

        (dto!.Status, dto.Note).Should().Be(("Dismissed", CreatePurchaseRequestFromStockAlertCommandHandler.CoveredNote));
    }

    [Fact]
    public async Task An_older_alert_without_max_still_creates_a_request_from_the_reorder_levels()
    {
        var legacy = new CreatePurchaseRequestFromStockAlertCommand
        {
            ProductId = Guid.NewGuid(), ProductName = "Old", CurrentQuantity = 40, ReorderPoint = 100, ReorderQuantity = 500
        };

        var dto = await AlertHandler().Handle(legacy, default);

        dto!.SuggestedQuantity.Should().Be(560);
        dto.ProductCode.Should().Be(legacy.ProductId.ToString("N"));
        _requests.Verify(r => r.GetOnOrderQuantityAsync(It.IsAny<string>(), default), Times.Never);
    }

    // ─── create orders ────────────────────────────────────────────────────

    private CreateOrdersFromPurchaseRequestsCommandHandler OrdersHandler() => new(_requests.Object, _store.Object, _user.Object);

    private PurchaseRequest Open(string code, Guid? preferredSupplier, decimal price = 0m, int suggested = 20)
    {
        var productId = Guid.NewGuid();
        var request = PurchaseRequest.CreateFromReplenishment(productId, code, code, new ReplenishmentNeed(0, 0, 10, suggested, 0));
        _requests.Setup(r => r.GetPreferredSupplierItemAsync(productId, default)).ReturnsAsync(
            preferredSupplier == null ? null : SupplierItem.Create(preferredSupplier.Value, Tenant, productId, price, 5));
        return request;
    }

    private void SetupSuppliers()
    {
        _store.Setup(s => s.GetSupplierAsync(SupplierA, default)).ReturnsAsync(Supplier.Create("A", "Supplier A", Tenant, currencyCode: "VND"));
        _store.Setup(s => s.GetSupplierAsync(SupplierB, default)).ReturnsAsync(Supplier.Create("B", "Supplier B", Tenant, currencyCode: "VND"));
    }

    [Fact]
    public async Task Preferred_requests_become_one_draft_order_per_supplier_and_the_rest_one_rfq()
    {
        SetupSuppliers();
        var shirt = Open("SHIRT", SupplierA, 90m);
        var pants = Open("PANTS", SupplierA, 210m, suggested: 30);
        var hat = Open("HAT", SupplierB, 40m);
        var sock = Open("SOCK", null);
        _requests.Setup(r => r.GetOpenByIdsAsync(null, default)).ReturnsAsync(new List<PurchaseRequest> { shirt, pants, hat, sock });
        _requests.Setup(r => r.GetSupplierIdsForProductsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default)).ReturnsAsync(new List<Guid>());
        _requests.Setup(r => r.GetActiveSupplierIdsAsync(default)).ReturnsAsync(new List<Guid> { SupplierA, SupplierB });
        var orders = new List<PurchaseOrder>();
        RequestForQuotation? rfq = null;
        _store.Setup(s => s.Add(It.IsAny<PurchaseOrder>())).Callback<PurchaseOrder>(orders.Add);
        _store.Setup(s => s.Add(It.IsAny<RequestForQuotation>())).Callback<RequestForQuotation>(r => rfq = r);

        var result = await OrdersHandler().Handle(new CreateOrdersFromPurchaseRequestsCommand(), default);

        result.PurchaseOrders.Should().HaveCount(2);
        var orderA = orders.Single(o => o.SupplierId == SupplierA);
        orderA.Status.Should().Be(ERP.PO.Domain.Enums.PurchaseOrderStatus.Draft);
        orderA.Items.Select(i => (i.ProductCode, i.Quantity, i.UnitPrice.Amount)).Should().BeEquivalentTo(new[] { ("SHIRT", 20m, 90m), ("PANTS", 30m, 210m) });
        result.PurchaseOrders.Single(o => o.SupplierId == SupplierA).Total.Should().Be(20 * 90m + 30 * 210m);
        orders.Single(o => o.SupplierId == SupplierB).Items.Single().ProductCode.Should().Be("HAT");

        result.Rfq!.Lines.Should().Be(1);
        rfq!.Lines.Single().ProductCode.Should().Be("SOCK");
        rfq.Invitations.Select(i => i.SupplierId).Should().BeEquivalentTo(new[] { SupplierA, SupplierB });

        shirt.Status.Should().Be(PurchaseRequestStatus.Ordered);
        shirt.PurchaseOrderId.Should().Be(orderA.Id);
        sock.Status.Should().Be(PurchaseRequestStatus.Quoting);
        sock.RfqId.Should().Be(rfq.Id);
        _store.Verify(s => s.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task A_request_that_is_no_longer_open_is_skipped_so_running_twice_orders_nothing_twice()
    {
        SetupSuppliers();
        var done = Open("SHIRT", SupplierA, 90m);
        done.LinkPurchaseOrder(Guid.NewGuid());
        // The repository only returns open requests, so an id that is already linked comes back as missing.
        _requests.Setup(r => r.GetOpenByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default)).ReturnsAsync(new List<PurchaseRequest>());

        var result = await OrdersHandler().Handle(new CreateOrdersFromPurchaseRequestsCommand { RequestIds = new() { done.Id } }, default);

        result.PurchaseOrders.Should().BeEmpty();
        result.Rfq.Should().BeNull();
        result.Skipped.Should().ContainSingle().Which.RequestId.Should().Be(done.Id);
        _store.Verify(s => s.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Without_any_supplier_to_ask_the_request_stays_open_and_is_reported()
    {
        var sock = Open("SOCK", null);
        _requests.Setup(r => r.GetOpenByIdsAsync(null, default)).ReturnsAsync(new List<PurchaseRequest> { sock });
        _requests.Setup(r => r.GetSupplierIdsForProductsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default)).ReturnsAsync(new List<Guid>());
        _requests.Setup(r => r.GetActiveSupplierIdsAsync(default)).ReturnsAsync(new List<Guid>());

        var result = await OrdersHandler().Handle(new CreateOrdersFromPurchaseRequestsCommand(), default);

        result.Rfq.Should().BeNull();
        result.Skipped.Should().ContainSingle();
        sock.IsOpen.Should().BeTrue();
    }

    [Fact]
    public async Task Marking_an_item_preferred_clears_the_flag_on_the_other_items_of_the_product()
    {
        var productId = Guid.NewGuid();
        var a = SupplierItem.Create(SupplierA, Tenant, productId, 90m, 5);
        var b = SupplierItem.Create(SupplierB, Tenant, productId, 80m, 5);
        a.SetPreferred(true);
        _requests.Setup(r => r.GetSupplierItemAsync(SupplierB, b.Id, default)).ReturnsAsync(b);
        _requests.Setup(r => r.GetSupplierItemsForProductAsync(productId, default)).ReturnsAsync(new List<SupplierItem> { a, b });

        var result = await new SetPreferredSupplierItemCommandHandler(_requests.Object)
            .Handle(new SetPreferredSupplierItemCommand { SupplierId = SupplierB, SupplierItemId = b.Id }, default);

        result.IsPreferred.Should().BeTrue();
        a.IsPreferred.Should().BeFalse();
        b.IsPreferred.Should().BeTrue();
    }
}
