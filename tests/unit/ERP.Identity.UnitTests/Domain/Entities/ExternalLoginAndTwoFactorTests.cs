using ERP.Identity.Domain.Constants;
using ERP.Identity.Domain.Entities;
using ERP.Identity.Domain.Events;
using FluentAssertions;
using Xunit;

namespace ERP.Identity.UnitTests.Domain.Entities;

public class ExternalLoginAndTwoFactorTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public void ExternalLogin_Create_CarriesTheTenant_AndRefusesAnEmptyOne()
    {
        var link = ExternalLogin.Create(Guid.NewGuid(), Tenant, "Google", "g-1", "a@b.test");

        link.TenantId.Should().Be(Tenant);
        link.IsActive.Should().BeTrue();
        link.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var act = () => ExternalLogin.Create(Guid.NewGuid(), Guid.Empty, "Google", "g-1", "a@b.test");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddExternalLogin_DefaultsToTheHomeTenant_AndAcceptsAnotherOne_WithoutDuplicates()
    {
        var other = Guid.NewGuid();
        var user = User.Create("a@b.test", "a@b.test", "hash", Tenant, UserType.Customer);

        user.AddExternalLogin("Google", "g-1", "a@b.test");
        user.AddExternalLogin("Google", "g-1", "a@b.test");          // same link again: ignored
        user.AddExternalLogin("Google", "g-1", "a@b.test", other);   // same account in another tenant: its own link

        user.ExternalLogins.Should().HaveCount(2);
        user.ExternalLogins.Select(l => l.TenantId).Should().BeEquivalentTo(new[] { Tenant, other });
    }

    [Fact]
    public void TwoFactorChallenge_KeepsTheMaskedDestination_ButTheEventCarriesTheRecipient()
    {
        var challenge = TwoFactorChallenge.Create(Guid.NewGuid(), Tenant, "hash", "Email",
            destination: "ab***@acme.test", recipient: "abcdef@acme.test", expiryMinutes: 5, maxAttempts: 3, plaintextCode: "123456");

        challenge.Destination.Should().Be("ab***@acme.test");
        var evt = challenge.DomainEvents.OfType<TwoFactorCodeGeneratedDomainEvent>().Single();
        evt.Destination.Should().Be("abcdef@acme.test");
        evt.Code.Should().Be("123456");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TwoFactorChallenge_WithoutARecipient_IsRefused(string recipient)
    {
        var act = () => TwoFactorChallenge.Create(Guid.NewGuid(), Tenant, "hash", "Sms",
            destination: "****", recipient: recipient, expiryMinutes: 5, maxAttempts: 3, plaintextCode: "123456");

        act.Should().Throw<ArgumentException>();
    }
}
