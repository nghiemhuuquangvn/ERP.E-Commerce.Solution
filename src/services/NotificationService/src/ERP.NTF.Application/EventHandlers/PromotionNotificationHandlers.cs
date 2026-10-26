using ERP.NTF.Application.Commands;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.NTF.Application.EventHandlers;

// Loyalty points and coupon messages (ROADMAP P1-6). Promotion's events know the account's user id only; the e-mail comes from the
// UserContact projection written from UserCreated. No contact on file: logged and skipped.

public class LoyaltyPointsEarnedIntegrationEvent : INotification
{
    public Guid AccountId { get; set; }
    public Guid UserId { get; set; }
    public int PointsEarned { get; set; }
    public int TotalAvailable { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class CouponRedeemedIntegrationEvent : INotification
{
    public Guid CouponId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public decimal DiscountAmount { get; set; }
    public DateTime OccurredOn { get; set; }
}

/// <summary>Sends a template to an account's e-mail; the dedup key makes a redelivered event send once.</summary>
public class UserNotifier
{
    private readonly IMediator _mediator;
    private readonly IRecipientRepository _recipients;
    private readonly ILogger<UserNotifier> _logger;

    public UserNotifier(IMediator mediator, IRecipientRepository recipients, ILogger<UserNotifier> logger)
    {
        _mediator = mediator;
        _recipients = recipients;
        _logger = logger;
    }

    public async Task<bool> SendAsync(Guid userId, string templateCode, string dedupSuffix, Dictionary<string, string> variables, CancellationToken ct)
    {
        var contact = await _recipients.GetUserContactAsync(userId, ct);
        if (contact == null || string.IsNullOrWhiteSpace(contact.Email))
        {
            _logger.LogInformation("No e-mail known for user {UserId}; {Template} was not sent", userId, templateCode);
            return false;
        }

        variables["customerName"] = contact.FullName;
        await _mediator.Send(new SendNotificationCommand
        {
            RecipientId = userId.ToString(),
            RecipientEmail = contact.Email,
            Channel = NotificationChannel.Email,
            TemplateCode = templateCode,
            TemplateVariables = variables,
            Priority = NotificationPriority.Normal,
            DedupKey = $"{templateCode}:{dedupSuffix}",
            SkipWhenOptedOut = true
        }, ct);
        return true;
    }
}

public class LoyaltyPointsEarnedIntegrationEventHandler : INotificationHandler<LoyaltyPointsEarnedIntegrationEvent>
{
    private readonly UserNotifier _notifier;
    public LoyaltyPointsEarnedIntegrationEventHandler(UserNotifier notifier) => _notifier = notifier;

    public Task Handle(LoyaltyPointsEarnedIntegrationEvent e, CancellationToken ct) =>
        _notifier.SendAsync(e.UserId, "LOYALTY_POINTS_EARNED", $"{e.UserId:N}:{e.Reason}",
            new()
            {
                ["points"] = e.PointsEarned.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
                ["totalPoints"] = e.TotalAvailable.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
                ["reason"] = Describe(e.Reason)
            }, ct);

    /// <summary>"SalesOrder:guid" is for the ledger, not for a customer.</summary>
    internal static string Describe(string reason) =>
        string.IsNullOrWhiteSpace(reason) ? "thank you" : reason.StartsWith("SalesOrder:", StringComparison.OrdinalIgnoreCase) ? "for your order" : reason;
}

public class CouponRedeemedIntegrationEventHandler : INotificationHandler<CouponRedeemedIntegrationEvent>
{
    private readonly UserNotifier _notifier;
    public CouponRedeemedIntegrationEventHandler(UserNotifier notifier) => _notifier = notifier;

    public Task Handle(CouponRedeemedIntegrationEvent e, CancellationToken ct) =>
        _notifier.SendAsync(e.UserId, "COUPON_REDEEMED", $"{e.CouponId:N}:{e.UserId:N}:{e.OccurredOn.Ticks}",
            new() { ["code"] = e.Code, ["discount"] = OrderNotifier.Money(e.DiscountAmount) }, ct);
}

// A resolved support case (CRM, ROADMAP P1-6): the event carries the contact's e-mail and name, so no lookup is needed.
public class CaseResolvedIntegrationEvent : INotification
{
    public Guid CaseId { get; set; }
    public Guid ContactId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactName { get; set; }
}

public class CaseResolvedIntegrationEventHandler : INotificationHandler<CaseResolvedIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<CaseResolvedIntegrationEventHandler> _logger;

    public CaseResolvedIntegrationEventHandler(IMediator mediator, ILogger<CaseResolvedIntegrationEventHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(CaseResolvedIntegrationEvent e, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(e.ContactEmail))
        {
            _logger.LogInformation("Case {CaseId} resolved but the contact has no e-mail; nothing sent", e.CaseId);
            return;
        }

        await _mediator.Send(new SendNotificationCommand
        {
            RecipientId = (e.ContactId == Guid.Empty ? e.CaseId : e.ContactId).ToString(),
            RecipientEmail = e.ContactEmail,
            Channel = NotificationChannel.Email,
            TemplateCode = "CASE_RESOLVED",
            TemplateVariables = new() { ["customerName"] = e.ContactName ?? string.Empty, ["title"] = e.Title },
            Priority = NotificationPriority.Normal,
            DedupKey = $"CASE_RESOLVED:{e.CaseId:N}",
            SkipWhenOptedOut = true
        }, ct);
    }
}
