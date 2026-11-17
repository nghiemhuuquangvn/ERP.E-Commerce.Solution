using ERP.Identity.Domain.Entities;
using ERP.Identity.Domain.Security;
using FluentAssertions;
using Xunit;

namespace ERP.Identity.UnitTests.Domain;

/// <summary>S2 / SEC-M3: idle timeout + absolute limit, back office vs customer (BR-SES-01..04).</summary>
public class SessionPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);
    private static readonly TenantAuthSettings Defaults = TenantAuthSettings.CreateDefault(Guid.NewGuid());

    [Fact]
    public void Defaults_BackOffice15MinutesIdle12Hours_Customer7Days30Days()
    {
        SessionPolicy.For(isCustomer: false, Defaults).Should().Be(new SessionPolicy(TimeSpan.FromMinutes(15), TimeSpan.FromHours(12)));
        SessionPolicy.For(isCustomer: true, Defaults).Should().Be(new SessionPolicy(TimeSpan.FromDays(7), TimeSpan.FromDays(30)));
    }

    [Fact]
    public void RefreshExpiry_IsTheIdleWindow_WhileTheSessionIsYoung()
    {
        var policy = SessionPolicy.For(false, Defaults);

        policy.RefreshExpiresAt(Now, sessionStartedAt: Now.AddHours(-1)).Should().Be(Now.AddMinutes(15));
    }

    [Fact]
    public void RefreshExpiry_NeverPassesTheAbsoluteEnd()
    {
        var policy = SessionPolicy.For(false, Defaults);
        var started = Now.AddHours(-12).AddMinutes(5); // 5 minutes left

        policy.RefreshExpiresAt(Now, started).Should().Be(started.AddHours(12));
    }

    [Fact]
    public void AccessToken_LivesAThirdOfTheIdleTime_CappedByTheConfiguredMaximum()
    {
        var policy = SessionPolicy.For(false, Defaults);

        policy.AccessTokenLifetime(TimeSpan.FromMinutes(60), Now, Now).Should().Be(TimeSpan.FromMinutes(5));
        policy.AccessTokenLifetime(TimeSpan.FromMinutes(3), Now, Now).Should().Be(TimeSpan.FromMinutes(3));
        SessionPolicy.For(true, Defaults).AccessTokenLifetime(TimeSpan.FromMinutes(60), Now, Now).Should().Be(TimeSpan.FromMinutes(60));
    }

    [Fact]
    public void AccessToken_NeverOutlivesTheSession_ButLivesAtLeastAMinute()
    {
        var policy = SessionPolicy.For(false, Defaults);

        policy.AccessTokenLifetime(TimeSpan.FromMinutes(60), Now, Now.AddHours(-12).AddMinutes(2)).Should().Be(TimeSpan.FromMinutes(2));
        policy.AccessTokenLifetime(TimeSpan.FromMinutes(60), Now, Now.AddHours(-13)).Should().Be(SessionPolicy.MinimumAccessLifetime);
    }

    [Fact]
    public void CustomerAccounts_AreRecognisedByTheirUserType()
    {
        var customer = User.Create("c@x.test", "c", "hash", Guid.NewGuid(), UserType.Customer);
        var staff = User.Create("s@x.test", "s", "hash", Guid.NewGuid(), UserType.Staff);

        SessionPolicy.IsCustomer(customer).Should().BeTrue();
        SessionPolicy.IsCustomer(staff).Should().BeFalse();
    }
}
