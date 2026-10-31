using ERP.PAY.Application.Interfaces;
using ERP.PAY.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.PAY.Application.Commands;

/// <summary>
/// A return was completed in Sales (ROADMAP P1-5): the customer gets <see cref="RefundAmount"/> back on the payment of the order, through the gateway.
/// </summary>
public class RefundReturnedOrderCommand : IRequest<RefundReturnedOrderResult>
{
    public Guid OrderId { get; set; }
    public decimal RefundAmount { get; set; }
}

public enum RefundReturnedOrderResult
{
    /// <summary>The refund was requested from the gateway (it may still have failed there: the refund row and the log say so).</summary>
    Requested,

    /// <summary>An earlier delivery of the same event already refunded (or is refunding) the return.</summary>
    AlreadyDone,

    /// <summary>The order has no payment in this service (paid in cash or at the POS, or never paid).</summary>
    NoPayment,

    /// <summary>The payment is not Captured / PartiallyRefunded, or is refunded already.</summary>
    NotRefundable,
}

/// <summary>
/// Finds the payment of the order and refunds <c>min(refund amount, what is still refundable)</c> with the existing refund flow (a refund is final only when the gateway
/// confirms it, and only then is <c>PaymentRefunded</c> published for Accounting). The reason text is fixed per order, so a redelivered event finds its refund and does nothing;
/// a refund the gateway declined does not count and is tried again.
/// </summary>
public class RefundReturnedOrderCommandHandler : IRequestHandler<RefundReturnedOrderCommand, RefundReturnedOrderResult>
{
    public static string ReasonFor(Guid orderId) => $"Return of order {orderId:N}";

    private readonly IPaymentUnitOfWork _uow;
    private readonly IMediator _mediator;
    private readonly ILogger<RefundReturnedOrderCommandHandler> _logger;

    public RefundReturnedOrderCommandHandler(IPaymentUnitOfWork uow, IMediator mediator, ILogger<RefundReturnedOrderCommandHandler> logger)
    {
        _uow = uow;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<RefundReturnedOrderResult> Handle(RefundReturnedOrderCommand request, CancellationToken ct)
    {
        var payment = await _uow.Payments.GetByOrderIdAsync(request.OrderId, ct);
        if (payment == null)
        {
            _logger.LogInformation("Returned order {OrderId} has no payment here — nothing to refund", request.OrderId);
            return RefundReturnedOrderResult.NoPayment;
        }

        var reason = ReasonFor(request.OrderId);
        if (payment.Refunds.Any(r => r.Reason == reason && r.Status != RefundStatus.Failed))
            return RefundReturnedOrderResult.AlreadyDone;

        if (payment.Status is not (PaymentStatus.Captured or PaymentStatus.PartiallyRefunded or PaymentStatus.PartiallyCaptured))
        {
            _logger.LogWarning("Returned order {OrderId}: payment {PaymentId} is {Status} — not refunded", request.OrderId, payment.Id, payment.Status);
            return RefundReturnedOrderResult.NotRefundable;
        }

        var refundable = payment.Collected - payment.Refunds.Where(r => r.Status == RefundStatus.Completed).Sum(r => r.Amount);
        var amount = Math.Round(Math.Min(request.RefundAmount, refundable), 2);
        if (amount <= 0m)
        {
            _logger.LogWarning("Returned order {OrderId}: nothing left to refund on payment {PaymentId}", request.OrderId, payment.Id);
            return RefundReturnedOrderResult.NotRefundable;
        }

        await _mediator.Send(new RefundPaymentCommand { PaymentId = payment.Id, Amount = amount, Reason = reason }, ct);
        return RefundReturnedOrderResult.Requested;
    }
}
