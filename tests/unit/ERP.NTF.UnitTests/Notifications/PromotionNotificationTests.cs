using ERP.NTF.Application.Commands;
using ERP.NTF.Application.EventHandlers;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MediatR;
using Moq;
using Xunit;

namespace ERP.NTF.UnitTests.Notifications;

/// <summary>ROADMAP P1-6: loyalty points and coupon messages go to the account's e-mail, once.</summary>
public class PromotionNotificationTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IRecipientRepository> _recipients = new();
    private readonly List<SendNotificationCommand> _sent = new();
    private readonly Guid _user = Guid.NewGuid();

    public PromotionNotificationTests()
    {
        _mediator.Setup(m => m.Send(It.IsAny<SendNotificationCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<ERP.NTF.Application.DTOs.NotificationDto>, CancellationToken>((c, _) => _sent.Add((SendNotificationCommand)c))
            .ReturnsAsync(new ERP.NTF.Application.DTOs.NotificationDto());
    }

    private UserNotifier Notifier() => new(_mediator.Object, _recipients.Object, NullLogger<UserNotifier>.Instance);

    private void KnownUser() =>
        _recipients.Setup(r => r.GetUserContactAsync(_user, default)).ReturnsAsync(new UserContact { UserId = _user, Email = "an@example.com", FullName = "An Nguyen" });

    [Fact]
    public async Task Points_earned_for_an_order_are_announced_in_plain_words()
    {
        KnownUser();

        await new LoyaltyPointsEarnedIntegrationEventHandler(Notifier()).Handle(new()
        { UserId = _user, PointsEarned = 1250, TotalAvailable = 3400, Reason = $"SalesOrder:{Guid.NewGuid()}" }, default);

        var cmd = _sent.Single();
        cmd.TemplateCode.Should().Be("LOYALTY_POINTS_EARNED");
        cmd.RecipientEmail.Should().Be("an@example.com");
        cmd.TemplateVariables!["points"].Should().Be("1,250");
        cmd.TemplateVariables["totalPoints"].Should().Be("3,400");
        cmd.TemplateVariables["reason"].Should().Be("for your order");   // not the ledger reason with a guid
        cmd.TemplateVariables["customerName"].Should().Be("An Nguyen");
    }

    [Fact]
    public async Task A_user_with_no_known_email_gets_nothing()
    {
        _recipients.Setup(r => r.GetUserContactAsync(_user, default)).ReturnsAsync((UserContact?)null);

        await new LoyaltyPointsEarnedIntegrationEventHandler(Notifier()).Handle(new() { UserId = _user, PointsEarned = 10 }, default);

        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task The_same_earn_gives_the_same_dedup_key_so_a_replay_is_one_message()
    {
        KnownUser();
        var h = new LoyaltyPointsEarnedIntegrationEventHandler(Notifier());
        var e = new LoyaltyPointsEarnedIntegrationEvent { UserId = _user, PointsEarned = 10, Reason = "SalesOrder:abc" };

        await h.Handle(e, default);
        await h.Handle(e, default);

        _sent.Select(c => c.DedupKey).Distinct().Should().HaveCount(1);
    }

    [Fact]
    public async Task A_coupon_use_is_announced_and_two_uses_are_two_messages()
    {
        KnownUser();
        var h = new CouponRedeemedIntegrationEventHandler(Notifier());
        var coupon = Guid.NewGuid();

        await h.Handle(new() { CouponId = coupon, Code = "SAVE10", UserId = _user, DiscountAmount = 50_000m, OccurredOn = new DateTime(2026, 10, 26, 9, 0, 0) }, default);
        await h.Handle(new() { CouponId = coupon, Code = "SAVE10", UserId = _user, DiscountAmount = 50_000m, OccurredOn = new DateTime(2026, 10, 27, 9, 0, 0) }, default);

        _sent.Should().HaveCount(2);
        _sent[0].TemplateCode.Should().Be("COUPON_REDEEMED");
        _sent[0].TemplateVariables!["discount"].Should().Be("50,000");
        _sent.Select(c => c.DedupKey).Should().OnlyHaveUniqueItems();
    }
}
