using ERP.Shared.Authorization;
using ERP.SO.Api.Controllers;
using ERP.SO.Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ERP.SO.UnitTests.Orders;

/// <summary>The credit limit list (Admin mobile app): customers that have a limit, with the name from their latest order and the credit still available.</summary>
public class CreditLimitListTests
{
    private static readonly Guid Ann = Guid.NewGuid();
    private static readonly Guid Binh = Guid.NewGuid();

    [Fact]
    public void Available_credit_is_the_limit_minus_the_open_balance_and_can_be_negative()
    {
        new CreditProfileRow(Ann, "Ann", 1000m, 250m, DateTime.UtcNow).AvailableCredit.Should().Be(750m);
        new CreditProfileRow(Ann, "Ann", 1000m, 1200m, DateTime.UtcNow).AvailableCredit.Should().Be(-200m);
        new CreditProfileRow(Ann, "", 0m, 0m, DateTime.UtcNow).AvailableCredit.Should().Be(0m);
    }

    [Fact]
    public void A_search_is_a_customer_id_or_part_of_a_name_and_blank_means_none()
    {
        CreditProfileSearch.Parse(null).Should().Be(new CreditProfileSearch(null, null));
        CreditProfileSearch.Parse("   ").Should().Be(new CreditProfileSearch(null, null));
        CreditProfileSearch.Parse("  BINH ").Should().Be(new CreditProfileSearch("binh", null));
        var id = Guid.NewGuid();
        CreditProfileSearch.Parse(id.ToString().ToUpper()).CustomerId.Should().Be(id);
    }

    private static CustomerSalesProfilesController Controller(Mock<ICustomerSalesProfileRepository> repo, params string[] permissions)
    {
        var user = new UserContext { UserId = "u1", TenantId = Guid.NewGuid().ToString(), Permissions = permissions };
        return new CustomerSalesProfilesController(repo.Object, user);
    }

    [Fact]
    public async Task The_list_answers_a_page_with_names_available_credit_and_the_total_of_all_matches()
    {
        var repo = new Mock<ICustomerSalesProfileRepository>();
        CreditProfileSearch? seen = null;
        repo.Setup(r => r.ListAsync(It.IsAny<CreditProfileSearch>(), 20, 20, It.IsAny<CancellationToken>()))
            .Callback<CreditProfileSearch, int, int, CancellationToken>((s, _, _, _) => seen = s)
            .ReturnsAsync((new List<CreditProfileRow> { new(Ann, "Ann Tran", 1000m, 900m, DateTime.UtcNow), new(Binh, "", 500m, 0m, DateTime.UtcNow) }, 47));

        var result = await Controller(repo, "sales_order:read").List("Tran", 2, 20);

        var page = result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<CreditProfilePage>().Subject;
        page.TotalCount.Should().Be(47);
        (page.PageNumber, page.PageSize).Should().Be((2, 20));
        page.Items.Select(i => i.CustomerName).Should().Equal("Ann Tran", "");
        page.Items.Select(i => i.AvailableCredit).Should().Equal(100m, 500m);
        seen.Should().Be(new CreditProfileSearch("tran", null));
    }

    [Fact]
    public async Task The_page_size_is_clamped_and_the_page_starts_at_one()
    {
        var repo = new Mock<ICustomerSalesProfileRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CreditProfileSearch>(), 0, 100, It.IsAny<CancellationToken>())).ReturnsAsync((new List<CreditProfileRow>(), 0));

        var result = await Controller(repo, "sales_order:read").List(null, 0, 5000);

        var page = result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<CreditProfilePage>().Subject;
        (page.PageNumber, page.PageSize).Should().Be((1, 100));
        repo.Verify(r => r.ListAsync(It.IsAny<CreditProfileSearch>(), 0, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Without_sales_order_read_the_list_is_refused_and_nothing_is_read()
    {
        var repo = new Mock<ICustomerSalesProfileRepository>(MockBehavior.Strict);
        var result = await Controller(repo, "crm:contacts:view").List(null, 1, 20);
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }
}
