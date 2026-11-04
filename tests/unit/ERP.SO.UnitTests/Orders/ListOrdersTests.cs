using ERP.Shared.Infrastructure.Exceptions;
using ERP.SO.Application.Handlers;
using ERP.SO.Application.Queries;
using ERP.SO.Domain.Entities;
using ERP.SO.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.SO.UnitTests.Orders;

/// <summary>The order list (Admin web, Admin mobile app): every filter is part of the paged query, and each row carries the order number.</summary>
public class ListOrdersTests
{
    private static SalesOrder Order(string no, string? day = null, string customer = "An")
    {
        var order = SalesOrder.Create(Guid.NewGuid(), customer, "an@example.com", new Address("1 Le Loi", "Quan 1", "Ho Chi Minh", "700000", "Vietnam", "Phuong Ben Nghe"),
            new List<OrderLineItem> { new() { ProductId = Guid.NewGuid(), ProductName = "Shirt", Quantity = 1, UnitPrice = 100m } }, orderNo: no);
        if (day != null) typeof(SalesOrder).GetProperty(nameof(SalesOrder.OrderDate))!.SetValue(order, DateTime.Parse(day + "T00:00:00Z").ToUniversalTime());
        return order;
    }

    private static List<SalesOrder> Orders() => new()
    {
        Order("SO-001", "2026-10-31"),
        Order("SO-002", "2026-11-01"),
        Order("SO-003", "2026-11-15", "Binh Tran"),
        Order("SO-004", "2026-11-30"),
        Order("SO-005", "2026-12-01"),
    };

    private static List<string> Numbers(OrderListFilter f) => f.Apply(Orders().AsQueryable()).Select(o => o.OrderNo).OrderBy(n => n).ToList();
    private static OrderListFilter Days(string? from, string? to, string? search = null) =>
        new(null, null, from is null ? null : DateOnly.Parse(from), to is null ? null : DateOnly.Parse(to), search);

    [Fact]
    public async Task The_filters_and_the_page_are_given_to_the_query_not_applied_to_a_page_afterwards()
    {
        var customer = Guid.NewGuid();
        var repo = new Mock<ISalesOrderRepository>();
        OrderListFilter? seen = null;
        repo.Setup(r => r.GetTotalCountAsync(It.IsAny<OrderListFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(116);
        repo.Setup(r => r.GetAllAsync(40, 20, It.IsAny<OrderListFilter>(), It.IsAny<CancellationToken>()))
            .Callback<int, int, OrderListFilter, CancellationToken>((_, _, f, _) => seen = f)
            .ReturnsAsync(new List<SalesOrder> { Order("SO-1") });
        repo.Setup(r => r.GetByCustomerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<SalesOrder>());

        var result = await new ListOrdersQueryHandler(repo.Object).Handle(new ListOrdersQuery
        {
            Skip = 40, Take = 20, CustomerId = customer, Status = "Submitted", DateFrom = new DateOnly(2026, 11, 1), DateTo = new DateOnly(2026, 11, 30), Search = "SO-1",
        }, default);

        result.Total.Should().Be(116);
        result.Items.Should().ContainSingle();
        seen.Should().Be(new OrderListFilter(customer, OrderStatus.Submitted, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), "SO-1"));
        repo.Verify(r => r.GetTotalCountAsync(seen!, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_row_carries_the_order_number()
    {
        var repo = new Mock<ISalesOrderRepository>();
        repo.Setup(r => r.GetTotalCountAsync(It.IsAny<OrderListFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        repo.Setup(r => r.GetAllAsync(0, 20, It.IsAny<OrderListFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<SalesOrder> { Order("SO-10482") });
        repo.Setup(r => r.GetByCustomerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<SalesOrder>());

        var result = await new ListOrdersQueryHandler(repo.Object).Handle(new ListOrdersQuery(), default);

        result.Items.Single().OrderNo.Should().Be("SO-10482");
    }

    [Fact]
    public async Task An_unknown_status_is_a_refusal_not_a_server_error_and_never_reaches_the_database()
    {
        var repo = new Mock<ISalesOrderRepository>(MockBehavior.Strict);
        var act = () => new ListOrdersQueryHandler(repo.Object).Handle(new ListOrdersQuery { Status = "NOPE" }, default);
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public void The_order_day_range_counts_both_end_days()
    {
        Numbers(Days("2026-11-01", "2026-11-30")).Should().Equal("SO-002", "SO-003", "SO-004");
        Numbers(Days("2026-11-15", "2026-11-15")).Should().Equal("SO-003");
        Numbers(Days("2026-11-30", null)).Should().Equal("SO-004", "SO-005");
        Numbers(Days(null, "2026-10-31")).Should().Equal("SO-001");
        Numbers(Days(null, null)).Should().HaveCount(5);
    }

    [Fact]
    public void A_start_after_the_end_is_refused()
    {
        var act = () => Numbers(Days("2026-12-01", "2026-11-01"));
        act.Should().Throw<BusinessRuleException>();
    }

    [Theory]
    [InlineData("so-003")]
    [InlineData("003")]
    [InlineData(" SO-003 ")]
    [InlineData("binh")]
    public void Search_finds_the_order_number_or_the_customer_ignoring_case(string text)
    {
        Numbers(Days(null, null, text)).Should().Equal("SO-003");
    }

    [Fact]
    public void A_blank_search_is_no_filter_and_no_match_is_empty()
    {
        Numbers(Days(null, null, "   ")).Should().HaveCount(5);
        Numbers(Days(null, null, "zzz")).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Submitted", OrderStatus.Submitted)]
    [InlineData("SUBMITTED", OrderStatus.Submitted)]
    [InlineData("partially_shipped", OrderStatus.PartiallyShipped)]
    [InlineData("ReturnRequested", OrderStatus.ReturnRequested)]
    public void The_status_is_read_the_way_clients_write_it(string text, OrderStatus expected)
    {
        OrderListFilter.ParseStatus(text).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void No_status_means_no_status_filter(string? text) => OrderListFilter.ParseStatus(text).Should().BeNull();

    [Theory]
    [InlineData("Nope")]
    [InlineData("99")]
    public void An_unknown_status_word_is_refused(string text)
    {
        var act = () => OrderListFilter.ParseStatus(text);
        act.Should().Throw<BusinessRuleException>();
    }
}
