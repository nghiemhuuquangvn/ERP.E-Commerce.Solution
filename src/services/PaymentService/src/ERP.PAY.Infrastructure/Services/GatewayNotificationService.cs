using ERP.PAY.Application.Commands;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Domain.Enums;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.PAY.Infrastructure.Services;

public class GatewayNotificationService : IGatewayNotificationService
{
    private readonly IPaymentRepository _payments;
    private readonly IPaymentProviderResolver _providers;
    private readonly IMediator _mediator;
    private readonly ILogger<GatewayNotificationService> _logger;

    public GatewayNotificationService(IPaymentRepository payments, IPaymentProviderResolver providers, IMediator mediator,
        ILogger<GatewayNotificationService> logger)
    {
        _payments = payments;
        _providers = providers;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<GatewayNotificationResult> ProcessAsync(string provider, GatewayCallback callback, CancellationToken ct = default)
    {
        var code = PaymentProviders.Normalize(provider);
        if (code == null || !PaymentProviders.IsGateway(code))
            throw new BusinessRuleException("Payment", $"'{provider}' is not an online payment gateway.");

        // The reference is read before it is trusted, only to find whose keys verify the message; the verified reference must match it.
        var peeked = _providers.Peek(code, callback);
        if (peeked == null)
            return Reject(code, NotificationOutcome.UnknownReference, "No payment reference");
        if (await _payments.FindTenantByTransactionRefAsync(peeked, ct) is not { } tenantId)
            return Reject(code, NotificationOutcome.UnknownReference, "Unknown payment reference");

        using var tenantScope = TenantScope.Begin(tenantId);

        ResolvedProvider resolved;
        try
        {
            resolved = await _providers.ResolveAsync(code, ct);
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogWarning("[{Provider}] Notification for {Ref} cannot be verified: {Reason}", code, peeked, ex.Message);
            return Reject(code, NotificationOutcome.NotVerifiable, ex.Message);
        }

        var verify = resolved.Provider.Verify(resolved.Credentials, callback);
        if (!verify.Success || !string.Equals(verify.TransactionRef, peeked, StringComparison.Ordinal))
        {
            _logger.LogWarning("[{Provider}] Invalid notification for {Ref}: {Error}", code, peeked, verify.Error);
            return Reject(code, NotificationOutcome.InvalidSignature, "Invalid signature");
        }

        try
        {
            var payment = await _mediator.Send(new ProcessGatewayCallbackCommand
            {
                TransactionRef = verify.TransactionRef,
                IsPaid = verify.IsPaid,
                GatewayTransactionId = verify.GatewayTransactionId,
                GatewayProvider = code,
                FailureReason = verify.Error,
                Amount = verify.Amount
            }, ct);

            var outcome = verify.IsPaid ? NotificationOutcome.Paid : NotificationOutcome.Failed;
            // A repeated "paid" for a payment that had failed (or the reverse) is answered as accepted: the gateway must stop retrying.
            return new GatewayNotificationResult(outcome, payment.OrderId, verify.Error, resolved.Provider.IpnResponse(true, "Confirm Success"));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("amount", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[{Provider}] Amount mismatch for {Ref}: {Error}", code, peeked, ex.Message);
            return new GatewayNotificationResult(NotificationOutcome.AmountMismatch, null, ex.Message, resolved.Provider.IpnResponse(false, "Amount mismatch"));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("[{Provider}] Notification for {Ref} could not be applied: {Error}", code, peeked, ex.Message);
            return new GatewayNotificationResult(NotificationOutcome.UnknownReference, null, ex.Message, resolved.Provider.IpnResponse(false, "Order not found"));
        }
    }

    private GatewayNotificationResult Reject(string code, NotificationOutcome outcome, string message) =>
        new(outcome, null, message, _providers.ReplyFor(code, accepted: false, message));
}
