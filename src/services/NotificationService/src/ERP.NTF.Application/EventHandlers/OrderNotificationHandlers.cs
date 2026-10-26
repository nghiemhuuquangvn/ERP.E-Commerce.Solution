using System.Globalization;
using ERP.NTF.Application.Commands;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.NTF.Application.EventHandlers;

// Customer notifications about an order and its payment (ROADMAP P1-6). The events other than SalesOrderCreated only know the order id;
// the customer's e-mail comes from the OrderRecipient projection written when the order was created.

public class SalesOrderShippedIntegrationEvent : INotification
{
    public Guid OrderId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
}

public class SalesOrderDeliveredIntegrationEvent : INotification
{
    public Guid OrderId { get; set; }
    public decimal TotalAmount { get; set; }
}

public class SalesOrderCancelledIntegrationEvent : INotification
{
    public Guid OrderId { get; set; }
    public string CancellationReason { get; set; } = string.Empty;
}

public class SalesOrderReturnedIntegrationEvent : INotification
{
    public Guid OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal RefundAmount { get; set; }
}

public class PaymentFailedIntegrationEvent : INotification
{
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string FailureReason { get; set; } = string.Empty;
}

public class PaymentRefundedIntegrationEvent : INotification
{
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? RefundId { get; set; }
    public decimal RefundAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Sends a template to the customer of an order. No recipient on file (an order from before the projection existed) or an empty
/// e-mail: logged and skipped - never a made-up address. The dedup key makes a redelivered event send once.
/// </summary>
public class OrderNotifier
{
    private readonly IMediator _mediator;
    private readonly IRecipientRepository _recipients;
    private readonly ILogger<OrderNotifier> _logger;

    public OrderNotifier(IMediator mediator, IRecipientRepository recipients, ILogger<OrderNotifier> logger)
    {
        _mediator = mediator;
        _recipients = recipients;
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        Guid orderId, string templateCode, string dedupSuffix, Dictionary<string, string> variables,
        NotificationPriority priority, CancellationToken ct, string? fallbackEmail = null)
    {
        var recipient = await _recipients.GetOrderRecipientAsync(orderId, ct);
        var email = !string.IsNullOrWhiteSpace(recipient?.Email) ? recipient!.Email : fallbackEmail;
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("No e-mail known for order {OrderId}; {Template} was not sent", orderId, templateCode);
            return false;
        }

        variables["customerName"] = recipient?.CustomerName ?? string.Empty;
        variables["orderNumber"] = !string.IsNullOrEmpty(recipient?.OrderNo) ? recipient!.OrderNo : orderId.ToString("N")[..8].ToUpperInvariant();

        await _mediator.Send(new SendNotificationCommand
        {
            RecipientId = (recipient?.CustomerId ?? orderId).ToString(),
            RecipientEmail = email,
            RecipientPhone = recipient?.Phone,
            Channel = NotificationChannel.Email,
            TemplateCode = templateCode,
            TemplateVariables = variables,
            Priority = priority,
            DedupKey = $"{templateCode}:{dedupSuffix}",
            SkipWhenOptedOut = true
        }, ct);
        return true;
    }

    public static string Money(decimal amount, string? currency = null) =>
        amount.ToString("N0", CultureInfo.InvariantCulture) + (string.IsNullOrWhiteSpace(currency) ? string.Empty : " " + currency);
}

public class SalesOrderShippedIntegrationEventHandler : INotificationHandler<SalesOrderShippedIntegrationEvent>
{
    private readonly OrderNotifier _notifier;
    public SalesOrderShippedIntegrationEventHandler(OrderNotifier notifier) => _notifier = notifier;

    public Task Handle(SalesOrderShippedIntegrationEvent e, CancellationToken ct) =>
        _notifier.SendAsync(e.OrderId, "ORDER_SHIPPED", e.OrderId.ToString("N"),
            new() { ["trackingNumber"] = e.TrackingNumber, ["carrier"] = e.Carrier }, NotificationPriority.Normal, ct);
}

public class SalesOrderDeliveredIntegrationEventHandler : INotificationHandler<SalesOrderDeliveredIntegrationEvent>
{
    private readonly OrderNotifier _notifier;
    public SalesOrderDeliveredIntegrationEventHandler(OrderNotifier notifier) => _notifier = notifier;

    public Task Handle(SalesOrderDeliveredIntegrationEvent e, CancellationToken ct) =>
        _notifier.SendAsync(e.OrderId, "ORDER_DELIVERED", e.OrderId.ToString("N"),
            new() { ["totalAmount"] = OrderNotifier.Money(e.TotalAmount) }, NotificationPriority.Normal, ct);
}

public class SalesOrderCancelledIntegrationEventHandler : INotificationHandler<SalesOrderCancelledIntegrationEvent>
{
    private readonly OrderNotifier _notifier;
    public SalesOrderCancelledIntegrationEventHandler(OrderNotifier notifier) => _notifier = notifier;

    public Task Handle(SalesOrderCancelledIntegrationEvent e, CancellationToken ct) =>
        _notifier.SendAsync(e.OrderId, "ORDER_CANCELLED", e.OrderId.ToString("N"),
            new() { ["reason"] = string.IsNullOrWhiteSpace(e.CancellationReason) ? "-" : e.CancellationReason }, NotificationPriority.High, ct);
}

public class SalesOrderReturnedIntegrationEventHandler : INotificationHandler<SalesOrderReturnedIntegrationEvent>
{
    private readonly OrderNotifier _notifier;
    public SalesOrderReturnedIntegrationEventHandler(OrderNotifier notifier) => _notifier = notifier;

    public Task Handle(SalesOrderReturnedIntegrationEvent e, CancellationToken ct) =>
        _notifier.SendAsync(e.OrderId, "ORDER_RETURNED", e.OrderId.ToString("N"),
            new() { ["refundAmount"] = OrderNotifier.Money(e.RefundAmount), ["reason"] = e.Reason }, NotificationPriority.Normal, ct);
}

public class PaymentFailedIntegrationEventHandler : INotificationHandler<PaymentFailedIntegrationEvent>
{
    private readonly OrderNotifier _notifier;
    public PaymentFailedIntegrationEventHandler(OrderNotifier notifier) => _notifier = notifier;

    public Task Handle(PaymentFailedIntegrationEvent e, CancellationToken ct) =>
        _notifier.SendAsync(e.OrderId, "PAYMENT_FAILED", e.PaymentId.ToString("N"),
            new() { ["amount"] = OrderNotifier.Money(e.Amount), ["reason"] = string.IsNullOrWhiteSpace(e.FailureReason) ? "-" : e.FailureReason },
            NotificationPriority.High, ct);
}

public class PaymentRefundedIntegrationEventHandler : INotificationHandler<PaymentRefundedIntegrationEvent>
{
    private readonly OrderNotifier _notifier;
    public PaymentRefundedIntegrationEventHandler(OrderNotifier notifier) => _notifier = notifier;

    // One notification per refund (a payment can be refunded in parts); old events without a refund id fall back to payment + amount.
    public Task Handle(PaymentRefundedIntegrationEvent e, CancellationToken ct) =>
        _notifier.SendAsync(e.OrderId, "PAYMENT_REFUNDED", (e.RefundId?.ToString("N") ?? $"{e.PaymentId:N}-{e.RefundAmount:0.##}"),
            new() { ["amount"] = OrderNotifier.Money(e.RefundAmount, e.Currency) }, NotificationPriority.Normal, ct);
}
