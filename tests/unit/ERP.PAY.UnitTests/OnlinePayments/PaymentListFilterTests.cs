using ERP.PAY.Domain.Entities;
using ERP.PAY.Domain.Enums;
using ERP.PAY.Infrastructure.Persistence;
using ERP.PAY.Infrastructure.Repositories;
using ERP.Shared.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.PAY.UnitTests.OnlinePayments;

/// <summary>Risk R17: the Payments screen filters by gateway and shows the payments flagged for review.</summary>
public class PaymentListFilterTests
{
    private readonly PaymentDbContext _db;

    public PaymentListFilterTests()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new PaymentDbContext(options, new TenantProvider(() => "11111111-1111-1111-1111-111111111111"));
    }

    private Payment Add(string provider, bool flagged = false)
    {
        var p = Payment.Initiate(Guid.NewGuid(), 100000m, PaymentProviders.MethodFor(provider), "VND", null, provider, Guid.NewGuid(), "SO-1");
        if (flagged) p.FlagForReview("refund the customer");
        _db.Payments.Add(p);
        _db.SaveChanges();
        return p;
    }

    [Fact]
    public async Task Filters_by_provider_ignoring_case_and_by_the_review_flag()
    {
        var momo = Add("MoMo");
        var momoFlagged = Add("MoMo", flagged: true);
        var zalo = Add("ZaloPay", flagged: true);
        Add("COD");
        var repo = new PaymentRepository(_db);

        var byProvider = await repo.GetPagedAsync(0, 20, null, "momo", false);
        var flagged = await repo.GetPagedAsync(0, 20, null, null, true);
        var both = await repo.GetPagedAsync(0, 20, null, "MoMo", true);
        var all = await repo.GetPagedAsync(0, 20, null, null, false);

        byProvider.Items.Select(p => p.Id).Should().BeEquivalentTo(new[] { momo.Id, momoFlagged.Id });
        flagged.Items.Select(p => p.Id).Should().BeEquivalentTo(new[] { momoFlagged.Id, zalo.Id });
        both.Items.Should().ContainSingle().Which.Id.Should().Be(momoFlagged.Id);
        all.Total.Should().Be(4);
    }
}
