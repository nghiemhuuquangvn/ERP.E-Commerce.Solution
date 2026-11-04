using ERP.PO.Application.Procurement;
using ERP.PO.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Xunit;

namespace ERP.PO.UnitTests.Procurement;

/// <summary>The day range / text of the RFQ, supplier invoice and vendor return lists, and their status words.</summary>
public class ListWindowTests
{
    [Fact]
    public void The_range_runs_from_the_first_instant_of_the_start_day_to_the_end_of_the_last_day()
    {
        var w = new ListWindow(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)).Checked();
        w.Start.Should().Be(new DateTime(2026, 11, 1));
        w.Before.Should().Be(new DateTime(2026, 12, 1));
    }

    [Fact]
    public void An_open_range_has_no_limits_and_a_blank_search_is_no_text()
    {
        ListWindow.None.Start.Should().BeNull();
        ListWindow.None.Before.Should().BeNull();
        new ListWindow(Search: "  ").Text.Should().BeNull();
        new ListWindow(Search: "  RFQ-7 ").Text.Should().Be("rfq-7");
    }

    [Fact]
    public void A_start_after_the_end_is_refused()
    {
        var act = () => new ListWindow(new DateOnly(2026, 12, 1), new DateOnly(2026, 11, 1)).Checked();
        act.Should().Throw<BusinessRuleException>();
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("DRAFT")]
    [InlineData("draft")]
    public void A_status_word_is_read_ignoring_case(string text) =>
        ListWindow.ParseStatus<RfqStatus>(text, "RFQ").Should().Be(RfqStatus.Draft);

    [Fact]
    public void No_status_means_no_filter_and_an_unknown_word_is_refused_not_ignored()
    {
        ListWindow.ParseStatus<RfqStatus>(null, "RFQ").Should().BeNull();
        ListWindow.ParseStatus<RfqStatus>(" ", "RFQ").Should().BeNull();
        var act = () => ListWindow.ParseStatus<RfqStatus>("Nope", "RFQ");
        act.Should().Throw<BusinessRuleException>();
    }
}
