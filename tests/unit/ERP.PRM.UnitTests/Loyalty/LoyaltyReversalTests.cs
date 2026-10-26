using ERP.PRM.Application.Commands;
using ERP.PRM.Application.EventHandlers;
using ERP.PRM.Application.Handlers;
using ERP.PRM.Application.Interfaces;
using ERP.PRM.Domain.Entities;
using ERP.PRM.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MediatR;
using Moq;
using Xunit;

namespace ERP.PRM.UnitTests.Loyalty;

/// <summary>ROADMAP P1-6: points earned for an order are earned once and taken back when the order is cancelled or returned.</summary>
public class LoyaltyReversalTests
{
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _order = Guid.NewGuid();
    private string Reason => $"SalesOrder:{_order}";

    [Fact]
    public void Reversing_takes_the_earned_points_back_and_the_tier_follows()
    {
        var account = LoyaltyAccount.Create(_user);
        account.EarnPoints(1_200, Reason);
        account.Tier.Should().Be(LoyaltyTier.Silver);

        var reversed = account.ReversePoints(Reason);

        reversed.Should().Be(1_200);
        account.TotalPoints.Should().Be(0);
        account.AvailablePoints.Should().Be(0);
        account.Tier.Should().Be(LoyaltyTier.Bronze);
        account.Transactions.Should().Contain(t => t.TransactionType == "Reverse" && t.Points == -1_200);
    }

    [Fact]
    public void A_second_reversal_of_the_same_order_changes_nothing()
    {
        var account = LoyaltyAccount.Create(_user);
        account.EarnPoints(500, Reason);
        account.ReversePoints(Reason);

        account.ReversePoints(Reason).Should().Be(0);
        account.Transactions.Count(t => t.TransactionType == "Reverse").Should().Be(1);
    }

    [Fact]
    public void Points_that_were_already_spent_do_not_push_the_balance_below_zero()
    {
        var account = LoyaltyAccount.Create(_user);
        account.EarnPoints(500, Reason);
        account.RedeemPoints(400);

        account.ReversePoints(Reason);

        account.AvailablePoints.Should().Be(0);
        account.TotalPoints.Should().Be(0);
    }

    [Fact]
    public void An_order_that_earned_nothing_reverses_nothing()
    {
        var account = LoyaltyAccount.Create(_user);
        account.EarnPoints(300, "SalesOrder:other");

        account.ReversePoints(Reason).Should().Be(0);
        account.AvailablePoints.Should().Be(300);
    }

    private (Mock<IPromotionUnitOfWork> Uow, Mock<ILoyaltyAccountRepository> Repo) Uow(LoyaltyAccount? account)
    {
        var repo = new Mock<ILoyaltyAccountRepository>();
        repo.Setup(r => r.GetByUserIdAsync(_user, default)).ReturnsAsync(account);
        var uow = new Mock<IPromotionUnitOfWork>();
        uow.SetupGet(u => u.LoyaltyAccounts).Returns(repo.Object);
        return (uow, repo);
    }

    [Fact]
    public async Task Earning_for_the_same_order_twice_earns_once()
    {
        var account = LoyaltyAccount.Create(_user);
        var (uow, _) = Uow(account);
        var handler = new EarnLoyaltyPointsCommandHandler(uow.Object);
        var command = new EarnLoyaltyPointsCommand { UserId = _user, Points = 800, Reason = Reason };

        await handler.Handle(command, default);
        await handler.Handle(command, default);

        account.AvailablePoints.Should().Be(800);
        uow.Verify(u => u.SaveChangesAsync(default), Times.Once); // the second call returns before saving
    }

    [Fact]
    public async Task The_reverse_command_saves_only_when_points_came_back()
    {
        var account = LoyaltyAccount.Create(_user);
        account.EarnPoints(800, Reason);
        var (uow, _) = Uow(account);
        var handler = new ReverseLoyaltyPointsCommandHandler(uow.Object);

        (await handler.Handle(new ReverseLoyaltyPointsCommand { UserId = _user, Reason = Reason }, default)).Should().Be(800);
        (await handler.Handle(new ReverseLoyaltyPointsCommand { UserId = _user, Reason = Reason }, default)).Should().Be(0);

        uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task An_unknown_customer_has_nothing_to_reverse()
    {
        var (uow, _) = Uow(null);
        var handler = new ReverseLoyaltyPointsCommandHandler(uow.Object);

        (await handler.Handle(new ReverseLoyaltyPointsCommand { UserId = _user, Reason = Reason }, default)).Should().Be(0);
    }

    [Fact]
    public async Task A_cancelled_or_returned_order_sends_the_reversal_for_its_reason()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<ReverseLoyaltyPointsCommand>(), default)).ReturnsAsync(120);
        var handler = new SalesOrderReversedForPromotionHandler(mediator.Object, NullLogger<SalesOrderReversedForPromotionHandler>.Instance);

        await handler.HandleAsync(new SalesOrderReversedIntegrationEvent(_order, _user), default);

        mediator.Verify(m => m.Send(It.Is<ReverseLoyaltyPointsCommand>(c => c.UserId == _user && c.Reason == Reason), default), Times.Once);
    }
}
