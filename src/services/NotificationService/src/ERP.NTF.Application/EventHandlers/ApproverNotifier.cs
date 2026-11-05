using ERP.NTF.Application.Commands;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.NTF.Application.EventHandlers;

/// <summary>
/// Tells the people who may approve something that it waits for them: one bell message per approver (and a push message when push is switched on),
/// each linked to the record. A person who turned bell messages off is skipped. The same fact never reaches the same person twice (the dedup key holds
/// the event, the record and the person), so a redelivered event sends nothing again.
/// </summary>
public class ApproverNotifier
{
    /// <summary>More approvers than this for one event is a sign of a wrong role setup; the rest are not notified (and the event is logged).</summary>
    public const int MaxApprovers = 200;

    private readonly IMediator _mediator;
    private readonly IApproverDirectory _directory;
    private readonly IPushStatus _push;
    private readonly ILogger<ApproverNotifier> _logger;

    public ApproverNotifier(IMediator mediator, IApproverDirectory directory, IPushStatus push, ILogger<ApproverNotifier> logger)
    {
        _mediator = mediator;
        _directory = directory;
        _push = push;
        _logger = logger;
    }

    public record Message(string Permission, string EventKey, Guid ObjectId, string Subject, string Body, string EntityType, string RouteKey);

    /// <summary>How many approvers were told.</summary>
    public async Task<int> NotifyAsync(Message message, CancellationToken ct)
    {
        var approvers = await _directory.GetUserIdsAsync(message.Permission, ct);
        if (approvers.Count == 0)
        {
            _logger.LogInformation("No one holds {Permission}: nobody to tell about {Event} {Object}", message.Permission, message.EventKey, message.ObjectId);
            return 0;
        }
        if (approvers.Count > MaxApprovers)
        {
            _logger.LogWarning("{Count} people hold {Permission}; only the first {Max} are told about {Event} {Object}", approvers.Count, message.Permission, MaxApprovers, message.EventKey, message.ObjectId);
            approvers = approvers.Take(MaxApprovers).ToList();
        }

        foreach (var approver in approvers)
        {
            var key = $"{message.EventKey}:{message.ObjectId:N}:{approver:N}";
            await _mediator.Send(Command(message, approver, NotificationChannel.InApp, key), ct);
            if (_push.IsEnabled)
                await _mediator.Send(Command(message, approver, NotificationChannel.Push, key + ":push"), ct);
        }
        return approvers.Count;
    }

    private static SendNotificationCommand Command(Message m, Guid approver, NotificationChannel channel, string dedupKey) => new()
    {
        RecipientId = approver.ToString(),
        Channel = channel,
        Subject = m.Subject,
        Body = m.Body,
        Priority = channel == NotificationChannel.Push ? NotificationPriority.High : NotificationPriority.Normal,
        DedupKey = dedupKey,
        SkipWhenOptedOut = true,
        EntityType = m.EntityType,
        EntityId = m.ObjectId.ToString(),
        RouteKey = m.RouteKey,
    };
}
