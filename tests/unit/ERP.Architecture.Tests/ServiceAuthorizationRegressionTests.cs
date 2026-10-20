using ERP.CHB.Application.Interfaces;
using ERP.CHB.Application.Queries;
using ERP.CHB.Domain.Entities;
using ERP.CHB.Domain.Enums;
using ERP.PRM.Application.Commands;
using ERP.Shared.Authorization;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>Regression tests for the 2026-10-20 service authorization review (phase 1).</summary>
public class ServiceAuthorizationRegressionTests
{
    [Fact]
    public void AddErpAuthorization_FallbackPolicyRequiresAuthenticatedUser()
    {
        var options = new ServiceCollection().AddLogging().AddErpAuthorization()
            .BuildServiceProvider().GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        options.FallbackPolicy.Should().NotBeNull();
        options.FallbackPolicy!.Requirements.Should().ContainSingle(r =>
            r is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task RedeemCoupon_UsesTheCallersId_NotTheBody()
    {
        RedeemCouponCommand? sent = null;
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<RedeemCouponCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((c, _) => sent = (RedeemCouponCommand)c)
            .ReturnsAsync(default(ERP.PRM.Application.DTOs.CouponValidationResult)!);
        var caller = new UserContext { UserId = "caller-id", TenantId = Guid.NewGuid().ToString(), ExpiresAt = DateTime.UtcNow.AddHours(1) };

        await new ERP.PRM.API.Controllers.PromotionsController(mediator.Object, caller)
            .RedeemCoupon(new RedeemCouponCommand { Code = "X", UserId = "victim-id", OrderAmount = 10 }, CancellationToken.None);

        sent!.UserId.Should().Be("caller-id");
    }

    private static (GetConversationHistoryQueryHandler Handler, Conversation Guest) GuestConversation(UserContext caller)
    {
        var guest = Conversation.Start(Guid.NewGuid(), ConversationChannel.Storefront, AiProvider.ChatGpt, userId: null, guestSessionId: "browser-session-1");
        var repo = new Mock<IConversationRepository>();
        repo.Setup(r => r.GetByIdAsync(guest.Id, It.IsAny<CancellationToken>())).ReturnsAsync(guest);
        return (new GetConversationHistoryQueryHandler(repo.Object, caller), guest);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("someone-elses-session")]
    public async Task GuestConversationHistory_WithoutMatchingSession_IsRejected(string? session)
    {
        var anyUser = new UserContext { UserId = Guid.NewGuid().ToString(), TenantId = Guid.NewGuid().ToString(), ExpiresAt = DateTime.UtcNow.AddHours(1) };
        var (handler, guest) = GuestConversation(anyUser);

        var act = () => handler.Handle(new GetConversationHistoryQuery(guest.Id, session), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GuestConversationHistory_WithMatchingSession_IsReturned()
    {
        var (handler, guest) = GuestConversation(new UserContext());

        var result = await handler.Handle(new GetConversationHistoryQuery(guest.Id, "browser-session-1"), CancellationToken.None);

        result.Conversation.Id.Should().Be(guest.Id);
    }
}
