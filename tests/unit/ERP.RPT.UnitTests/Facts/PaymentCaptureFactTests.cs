using ERP.RPT.Infrastructure.EventConsumers;
using ERP.RPT.Infrastructure.Persistence;
using ERP.Shared.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.RPT.UnitTests.Facts;

/// <summary>ROADMAP R43: a cash-on-delivery payment sends one PaymentProcessed per parcel; each part counts once.</summary>
public class PaymentCaptureFactTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private sealed class FixedTenant : ITenantProvider
    {
        public Guid? CurrentTenantId => Tenant;
    }

    private static ReportingDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ReportingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new FixedTenant());

    private static Task<bool> Apply(ReportingDbContext db, Guid payment, Guid capture, decimal amount) =>
        ReportingPayments.ApplyProcessedAsync(db, payment, capture, Guid.NewGuid(), amount, "VND", "Cash", new DateTime(2026, 10, 28), default);

    [Fact]
    public async Task Two_parcels_of_one_payment_add_up_in_one_fact_and_a_replay_adds_nothing()
    {
        await using var db = NewDb();
        var payment = Guid.NewGuid();
        var parcel1 = Guid.NewGuid();
        var parcel2 = Guid.NewGuid();

        (await Apply(db, payment, parcel1, 100_000m)).Should().BeTrue();
        (await Apply(db, payment, parcel2, 60_000m)).Should().BeTrue();
        (await Apply(db, payment, parcel1, 100_000m)).Should().BeFalse();   // the same message again

        (await db.FactPayments.SingleAsync()).Amount.Should().Be(160_000m);
        (await db.FactPaymentCaptures.CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData("4", "Cash")]
    [InlineData("3", "EWallet")]
    [InlineData("Cash", "Cash")]
    [InlineData("", "")]
    public void The_payment_method_arrives_as_a_number_or_a_name(string raw, string expected) =>
        ReportingPayments.MethodName(raw).Should().Be(expected);

    [Fact]
    public void A_numeric_method_is_read_as_text_instead_of_throwing()
    {
        var root = System.Text.Json.JsonDocument.Parse("{\"Method\":4,\"Currency\":\"VND\"}").RootElement;
        JDoc.GetString(root, "Method").Should().Be("4");
        JDoc.GetString(root, "Currency").Should().Be("VND");
        JDoc.GetString(root, "Missing").Should().Be("");
    }

    [Fact]
    public async Task A_payment_collected_in_one_go_is_recorded_once_as_before()
    {
        await using var db = NewDb();
        var payment = Guid.NewGuid();

        (await Apply(db, payment, Guid.Empty, 90_000m)).Should().BeTrue();
        (await Apply(db, payment, Guid.Empty, 90_000m)).Should().BeFalse();

        (await db.FactPayments.SingleAsync()).Amount.Should().Be(90_000m);
        (await db.FactPaymentCaptures.CountAsync()).Should().Be(0);
    }
}
