using AutoMapper;
using ERP.PO.Application.Features.PurchaseOrders;
using ERP.PO.Application.Features.PurchaseOrders.Handlers;
using ERP.PO.Application.Features.PurchaseOrders.Queries;
using ERP.PO.Application.Mappings;
using ERP.PO.Domain.Entities;
using ERP.PO.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.PO.UnitTests.PurchaseOrders;

/// <summary>The purchase order list filter (status, order-day range, number / supplier search) and the page answer (total counts every match).</summary>
public class PurchaseOrderListTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid SupplierA = Guid.NewGuid();

    private static void Set(object target, string property, object? value) =>
        target.GetType().GetProperty(property)!.SetValue(target, value);

    private static PurchaseOrder Order(string number, string orderDay, string supplierName = "Acme Ltd", Guid? supplierId = null, bool deleted = false)
    {
        var po = PurchaseOrder.Create(number, supplierId ?? SupplierA, Tenant);
        Set(po, nameof(PurchaseOrder.OrderDate), DateTime.Parse(orderDay + "T00:00:00Z").ToUniversalTime());
        var supplier = (Supplier)Activator.CreateInstance(typeof(Supplier), true)!;
        Set(supplier, nameof(Supplier.Name), supplierName);
        Set(po, nameof(PurchaseOrder.Supplier), supplier);
        if (deleted) Set(po, nameof(PurchaseOrder.IsDeleted), true);
        return po;
    }

    private static List<PurchaseOrder> Orders() => new()
    {
        Order("PO-001", "2026-10-31"),
        Order("PO-002", "2026-11-01"),
        Order("PO-003", "2026-11-15", "Beta Trading"),
        Order("PO-004", "2026-11-30"),
        Order("PO-005", "2026-12-01"),
        Order("PO-GONE", "2026-11-10", deleted: true),
    };

    private static List<string> Numbers(PurchaseOrderFilter f) => f.Apply(Orders().AsQueryable()).Select(p => p.PONumber).OrderBy(n => n).ToList();

    private static PurchaseOrderFilter Day(string? from, string? to, string? search = null, PurchaseOrderStatus? status = null) =>
        new(null, status, from is null ? null : DateOnly.Parse(from), to is null ? null : DateOnly.Parse(to), search);

    [Fact]
    public void Without_a_filter_every_order_that_is_not_deleted_matches()
    {
        Numbers(Day(null, null)).Should().Equal("PO-001", "PO-002", "PO-003", "PO-004", "PO-005");
    }

    [Fact]
    public void The_order_day_range_counts_both_end_days()
    {
        Numbers(Day("2026-11-01", "2026-11-30")).Should().Equal("PO-002", "PO-003", "PO-004");
    }

    [Fact]
    public void One_day_range_and_open_ended_ranges()
    {
        Numbers(Day("2026-11-15", "2026-11-15")).Should().Equal("PO-003");
        Numbers(Day("2026-11-30", null)).Should().Equal("PO-004", "PO-005");
        Numbers(Day(null, "2026-10-31")).Should().Equal("PO-001");
    }

    [Fact]
    public void A_start_after_the_end_is_refused()
    {
        var act = () => Numbers(Day("2026-12-01", "2026-11-01"));
        act.Should().Throw<BusinessRuleException>();
    }

    [Theory]
    [InlineData("po-003")]
    [InlineData("003")]
    [InlineData("  PO-003 ")]
    [InlineData("beta")]
    public void Search_finds_the_number_or_the_supplier_ignoring_case(string text)
    {
        Numbers(Day(null, null, text)).Should().Equal("PO-003");
    }

    [Fact]
    public void Search_with_no_match_is_empty_and_a_blank_search_is_no_filter()
    {
        Numbers(Day(null, null, "zzz")).Should().BeEmpty();
        Numbers(Day(null, null, "  ")).Should().HaveCount(5);
    }

    [Fact]
    public void Another_supplier_is_filtered_out()
    {
        var f = new PurchaseOrderFilter(Guid.NewGuid(), null, null, null, null);
        f.Apply(Orders().AsQueryable()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("SUBMITTED", PurchaseOrderStatus.Submitted)]
    [InlineData("Submitted", PurchaseOrderStatus.Submitted)]
    [InlineData("submitted", PurchaseOrderStatus.Submitted)]
    [InlineData("PENDING_FINANCE_APPROVAL", PurchaseOrderStatus.PendingFinanceApproval)]
    [InlineData("PendingFinanceApproval", PurchaseOrderStatus.PendingFinanceApproval)]
    [InlineData("PARTIALLY_RECEIVED", PurchaseOrderStatus.PartiallyReceived)]
    public void The_status_is_read_the_way_clients_write_it(string text, PurchaseOrderStatus expected)
    {
        PurchaseOrderFilter.ParseStatus(text).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void No_status_means_no_status_filter(string? text)
    {
        PurchaseOrderFilter.ParseStatus(text).Should().BeNull();
    }

    [Theory]
    [InlineData("SHIPPED")]
    [InlineData("99")]
    public void An_unknown_status_is_refused_instead_of_returning_everything(string text)
    {
        var act = () => PurchaseOrderFilter.ParseStatus(text);
        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void The_status_filter_keeps_only_that_status()
    {
        var orders = Orders();
        Set(orders[0], nameof(PurchaseOrder.Status), PurchaseOrderStatus.Submitted);
        var f = Day(null, null, status: PurchaseOrderStatus.Submitted);
        f.Apply(orders.AsQueryable()).Select(p => p.PONumber).Should().Equal("PO-001");
    }

    private static GetPurchaseOrdersQueryHandler Handler(Mock<IPurchaseOrderSearch> search)
    {
        var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>()).CreateMapper();
        return new GetPurchaseOrdersQueryHandler(search.Object, mapper);
    }

    [Fact]
    public async Task The_page_answer_carries_the_total_and_the_clamped_paging()
    {
        var search = new Mock<IPurchaseOrderSearch>();
        PurchaseOrderFilter? seen = null;
        int seenPage = 0, seenSize = 0;
        search.Setup(s => s.SearchAsync(It.IsAny<PurchaseOrderFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<PurchaseOrderFilter, int, int, CancellationToken>((f, p, z, _) => { seen = f; seenPage = p; seenSize = z; })
            .ReturnsAsync((new List<PurchaseOrder> { Order("PO-009", "2026-11-02") }, 41));

        var page = await Handler(search).Handle(new GetPurchaseOrdersQuery
        {
            Status = "PENDING_FINANCE_APPROVAL", DateFrom = new DateOnly(2026, 11, 1), DateTo = new DateOnly(2026, 11, 30), Search = "PO-9", PageNumber = 0, PageSize = 500,
        }, CancellationToken.None);

        page.TotalCount.Should().Be(41);
        page.Items.Should().ContainSingle().Which.PONumber.Should().Be("PO-009");
        page.PageNumber.Should().Be(1);
        page.PageSize.Should().Be(100);
        seenPage.Should().Be(1);
        seenSize.Should().Be(100);
        seen!.Status.Should().Be(PurchaseOrderStatus.PendingFinanceApproval);
        seen.From.Should().Be(new DateOnly(2026, 11, 1));
        seen.To.Should().Be(new DateOnly(2026, 11, 30));
        seen.Search.Should().Be("PO-9");
    }

    [Fact]
    public async Task An_unknown_status_never_reaches_the_database()
    {
        var search = new Mock<IPurchaseOrderSearch>(MockBehavior.Strict);
        var act = () => Handler(search).Handle(new GetPurchaseOrdersQuery { Status = "NOPE" }, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }
}
