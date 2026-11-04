using ERP.Shared.Infrastructure.Exceptions;
using ERP.SO.Application.Handlers;
using ERP.SO.Application.Interfaces;
using ERP.SO.Application.Queries;
using ERP.SO.Domain.Entities;
using ERP.SO.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.SO.UnitTests.Orders;

/// <summary>The quotation list: customer, status, day range and number / customer search are all part of the paged query (R124: the customer filter used to run after paging).</summary>
public class ListQuotationsTests
{
    private static SalesQuotation Quote(string no, string day, string customer = "An", Guid? customerId = null, QuotationStatus status = QuotationStatus.Draft)
    {
        var q = SalesQuotation.Create(customerId ?? Guid.NewGuid(), customer, "an@example.com",
            new List<QuotationLineItem> { new(Guid.NewGuid(), "Shirt", 1, 100m) }, DateTime.UtcNow.AddDays(30), no);
        q.CreatedAt = DateTime.Parse(day + "T00:00:00Z").ToUniversalTime();
        typeof(SalesQuotation).GetProperty(nameof(SalesQuotation.Status))!.SetValue(q, status);
        return q;
    }

    private static List<SalesQuotation> Quotes() => new()
    {
        Quote("Q-001", "2026-10-31"),
        Quote("Q-002", "2026-11-01"),
        Quote("Q-003", "2026-11-15", "Binh Tran", status: QuotationStatus.Sent),
        Quote("Q-004", "2026-11-30"),
        Quote("Q-005", "2026-12-01"),
    };

    private static List<string> Numbers(QuotationListFilter f) => f.Apply(Quotes().AsQueryable()).Select(q => q.QuotationNo).OrderBy(n => n).ToList();

    [Fact]
    public void The_day_range_counts_both_end_days()
    {
        Numbers(new QuotationListFilter(From: new DateOnly(2026, 11, 1), To: new DateOnly(2026, 11, 30))).Should().Equal("Q-002", "Q-003", "Q-004");
        Numbers(new QuotationListFilter(From: new DateOnly(2026, 11, 15), To: new DateOnly(2026, 11, 15))).Should().Equal("Q-003");
    }

    [Fact]
    public void Status_and_search_narrow_the_list()
    {
        Numbers(new QuotationListFilter(Status: QuotationStatus.Sent)).Should().Equal("Q-003");
        Numbers(new QuotationListFilter(Search: " q-004 ")).Should().Equal("Q-004");
        Numbers(new QuotationListFilter(Search: "binh")).Should().Equal("Q-003");
        Numbers(new QuotationListFilter()).Should().HaveCount(5);
    }

    [Fact]
    public void The_customer_filter_is_part_of_the_query()
    {
        var customer = Guid.NewGuid();
        var list = new List<SalesQuotation> { Quote("Q-A", "2026-11-01", customerId: customer), Quote("Q-B", "2026-11-01") };
        new QuotationListFilter(CustomerId: customer).Apply(list.AsQueryable()).Select(q => q.QuotationNo).Should().Equal("Q-A");
    }

    [Fact]
    public void A_start_after_the_end_and_an_unknown_status_are_refused()
    {
        var back = () => Numbers(new QuotationListFilter(From: new DateOnly(2026, 12, 1), To: new DateOnly(2026, 11, 1)));
        back.Should().Throw<BusinessRuleException>();
        var unknown = () => QuotationListFilter.ParseStatus("Nope");
        unknown.Should().Throw<BusinessRuleException>();
        QuotationListFilter.ParseStatus("SENT").Should().Be(QuotationStatus.Sent);
        QuotationListFilter.ParseStatus(null).Should().BeNull();
    }

    [Fact]
    public async Task The_handler_gives_the_filter_and_page_to_the_query_and_keeps_the_total()
    {
        var repo = new Mock<ISalesQuotationRepository>();
        QuotationListFilter? seen = null;
        repo.Setup(r => r.GetTotalCountAsync(It.IsAny<QuotationListFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(57);
        repo.Setup(r => r.GetAllAsync(20, 20, It.IsAny<QuotationListFilter>(), It.IsAny<CancellationToken>()))
            .Callback<int, int, QuotationListFilter, CancellationToken>((_, _, f, _) => seen = f)
            .ReturnsAsync(new List<SalesQuotation> { Quote("Q-9", "2026-11-02") });

        var result = await new ListQuotationsQueryHandler(repo.Object).Handle(new ListQuotationsQuery
        {
            Skip = 20, Take = 20, Status = "Sent", DateFrom = new DateOnly(2026, 11, 1), DateTo = new DateOnly(2026, 11, 30), Search = "Q-9",
        }, default);

        result.Total.Should().Be(57);
        result.Items.Should().ContainSingle().Which.QuotationDate.Should().Be(DateTime.Parse("2026-11-02T00:00:00Z").ToUniversalTime());
        seen.Should().Be(new QuotationListFilter(null, QuotationStatus.Sent, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), "Q-9"));
    }
}
