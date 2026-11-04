using ERP.PO.Application.DTOs;
using ERP.PO.Application.Procurement;
using ERP.PO.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.PO.UnitTests.Procurement;

/// <summary>The RFQ, supplier invoice and vendor return lists answer a page with the total of all matches; the page size is clamped.</summary>
public class ProcurementListPagingTests
{
    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-4, 500, 1, 100)]
    [InlineData(3, 20, 3, 20)]
    public void The_page_number_and_size_are_clamped(int number, int size, int expectedNumber, int expectedSize)
    {
        PagedList<int>.Clamp(number, size).Should().Be((expectedNumber, expectedSize));
    }

    [Fact]
    public void A_page_can_be_mapped_without_losing_its_total()
    {
        var page = new PagedList<int>(new[] { 1, 2 }, 41, 3, 2).Map(i => i * 10);
        page.Items.Should().Equal(10, 20);
        (page.TotalCount, page.PageNumber, page.PageSize).Should().Be((41, 3, 2));
    }

    [Fact]
    public async Task Rfqs_come_back_with_the_total_and_the_window_and_status_go_to_the_store()
    {
        var store = new Mock<IProcurementStore>();
        ListWindow? seen = null;
        RfqStatus? seenStatus = null;
        int seenNumber = 0, seenSize = 0;
        store.Setup(s => s.ListRfqsAsync(It.IsAny<RfqStatus?>(), It.IsAny<ListWindow>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<RfqStatus?, ListWindow, int, int, CancellationToken>((st, w, n, z, _) => { seenStatus = st; seen = w; seenNumber = n; seenSize = z; })
            .ReturnsAsync((new List<RequestForQuotation>(), 57));

        var page = await new ListRfqsQueryHandler(store.Object).Handle(
            new ListRfqsQuery("sent", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), "RFQ", 0, 500), CancellationToken.None);

        page.TotalCount.Should().Be(57);
        (page.PageNumber, page.PageSize).Should().Be((1, 100));
        (seenNumber, seenSize).Should().Be((1, 100));
        seenStatus.Should().Be(RfqStatus.Sent);
        seen.Should().Be(new ListWindow(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), "RFQ"));
    }

    [Fact]
    public async Task An_unknown_status_never_reaches_the_store()
    {
        var store = new Mock<IProcurementStore>(MockBehavior.Strict);
        var act = () => new ListRfqsQueryHandler(store.Object).Handle(new ListRfqsQuery("nope"), CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Invoices_and_returns_pass_their_page_to_the_store_and_keep_the_total()
    {
        var store = new Mock<IProcurementStore>();
        store.Setup(s => s.ListInvoicesAsync(It.IsAny<SupplierInvoiceStatus?>(), It.IsAny<Guid?>(), It.IsAny<ListWindow>(), 2, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<SupplierInvoice>(), 45));
        store.Setup(s => s.GetSupplierNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<Guid, string>());
        store.Setup(s => s.ListReturnsAsync(It.IsAny<Guid?>(), It.IsAny<VendorReturnStatus?>(), It.IsAny<ListWindow>(), 3, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<VendorReturn>(), 23));

        var invoices = await new ListSupplierInvoicesQueryHandler(store.Object).Handle(new ListSupplierInvoicesQuery(null, null, PageNumber: 2), CancellationToken.None);
        var returns = await new ListVendorReturnsQueryHandler(store.Object).Handle(new ListVendorReturnsQuery(null, null, PageNumber: 3, PageSize: 10), CancellationToken.None);

        invoices.TotalCount.Should().Be(45);
        invoices.PageNumber.Should().Be(2);
        returns.TotalCount.Should().Be(23);
        (returns.PageNumber, returns.PageSize).Should().Be((3, 10));
    }
}
