using ERP.PAY.Application.DTOs;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Domain.Entities;
using ERP.PAY.Domain.Enums;
using MediatR;

namespace ERP.PAY.Application.Handlers;

// ── Checkout: the customer pays an order they just placed ──────────────────────

/// <summary>Starts (or restarts, while the payment is still Pending) the gateway payment of an order and returns where to send the customer.</summary>
public class StartCheckoutCommand : IRequest<CheckoutResult>
{
    public Guid OrderId { get; set; }
    /// <summary>The signed-in user (a customer pays for their own order).</summary>
    public Guid CallerId { get; set; }
    /// <summary>Staff with payment rights may start it for any order.</summary>
    public bool CallerIsStaff { get; set; }
    /// <summary>Public base URL of this tenant's host, e.g. https://shop.example.com — where the gateway sends the browser and its notifications.</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}

public record CheckoutResult(string PaymentUrl, string TransactionRef, string Provider);

public class StartCheckoutCommandHandler : IRequestHandler<StartCheckoutCommand, CheckoutResult>
{
    /// <summary>The payment is created by the order saga a moment after the order: wait for it, but not for ever.</summary>
    public static int WaitAttempts = 20;
    public static TimeSpan WaitStep = TimeSpan.FromMilliseconds(500);

    private readonly IPaymentUnitOfWork _uow;
    private readonly IPaymentProviderResolver _providers;

    public StartCheckoutCommandHandler(IPaymentUnitOfWork uow, IPaymentProviderResolver providers)
    {
        _uow = uow;
        _providers = providers;
    }

    public async Task<CheckoutResult> Handle(StartCheckoutCommand request, CancellationToken ct)
    {
        var payment = await WaitForPaymentAsync(request.OrderId, ct)
            ?? throw new InvalidOperationException("The payment of this order is not ready yet. Try again in a moment.");

        // Someone else's order looks the same as a missing one.
        if (!request.CallerIsStaff && payment.CustomerId != request.CallerId)
            throw new KeyNotFoundException($"No payment found for order {request.OrderId}.");

        if (!PaymentProviders.IsGateway(payment.Provider))
            throw new InvalidOperationException(payment.Provider == PaymentProviders.Cod
                ? "This order is paid on delivery."
                : "This order was not placed with an online payment method.");
        if (payment.Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"This payment is {payment.Status}; it cannot be started again.");

        var resolved = await _providers.ResolveAsync(payment.Provider!, ct);
        var now = DateTime.UtcNow;
        var transactionRef = resolved.Provider.NewTransactionRef(payment.Id, now);
        var baseUrl = request.PublicBaseUrl.TrimEnd('/');
        var code = resolved.Provider.Code.ToLowerInvariant();

        var created = await resolved.Provider.CreatePaymentAsync(resolved.Credentials, new GatewayPaymentRequest(
            payment.Id, payment.Amount, payment.Currency, transactionRef,
            $"Order {payment.OrderNumber ?? payment.OrderId.ToString("N")[..8]}",
            $"{baseUrl}/api/v1/payment-gateway/callback/{code}",
            $"{baseUrl}/api/v1/payment-gateway/ipn/{code}", now), ct);

        if (!created.Success)
            throw new InvalidOperationException(created.Error ?? "The payment gateway refused the request.");

        // Only now: a failed attempt must not replace the reference of one the customer may still complete.
        payment.AssignTransactionRef(transactionRef);
        await _uow.Payments.UpdateAsync(payment, ct);
        await _uow.SaveChangesAsync(ct);
        return new CheckoutResult(created.PaymentUrl, transactionRef, resolved.Provider.Code);
    }

    private async Task<Payment?> WaitForPaymentAsync(Guid orderId, CancellationToken ct)
    {
        for (var i = 0; i < WaitAttempts; i++)
        {
            var payment = await _uow.Payments.GetByOrderIdAsync(orderId, ct);
            if (payment != null) return payment;
            await Task.Delay(WaitStep, ct);
        }
        return null;
    }
}

// ── Status of the payment of an order (result page, mobile polling) ────────────

public class GetOrderPaymentStatusQuery : IRequest<OrderPaymentStatusDto?>
{
    public Guid OrderId { get; set; }
    public Guid CallerId { get; set; }
    public bool CallerIsStaff { get; set; }
}

public record OrderPaymentStatusDto(Guid PaymentId, Guid OrderId, string Status, string? Provider, string Method, decimal Amount, string Currency,
    string? FailureReason, DateTime? CapturedAt);

public class GetOrderPaymentStatusQueryHandler : IRequestHandler<GetOrderPaymentStatusQuery, OrderPaymentStatusDto?>
{
    private readonly IPaymentUnitOfWork _uow;
    public GetOrderPaymentStatusQueryHandler(IPaymentUnitOfWork uow) => _uow = uow;

    public async Task<OrderPaymentStatusDto?> Handle(GetOrderPaymentStatusQuery request, CancellationToken ct)
    {
        var payment = await _uow.Payments.GetByOrderIdAsync(request.OrderId, ct);
        if (payment == null || (!request.CallerIsStaff && payment.CustomerId != request.CallerId)) return null;

        return new OrderPaymentStatusDto(payment.Id, payment.OrderId, payment.Status.ToString(), payment.Provider, payment.Method.ToString(),
            payment.Amount, payment.Currency, payment.FailureReason, payment.CapturedAt);
    }
}

// ── The methods a customer can choose at checkout ──────────────────────────────

public class GetPaymentMethodsQuery : IRequest<IReadOnlyList<PaymentMethodOption>> { }

public class GetPaymentMethodsQueryHandler : IRequestHandler<GetPaymentMethodsQuery, IReadOnlyList<PaymentMethodOption>>
{
    private readonly IPaymentProviderResolver _providers;
    public GetPaymentMethodsQueryHandler(IPaymentProviderResolver providers) => _providers = providers;

    public Task<IReadOnlyList<PaymentMethodOption>> Handle(GetPaymentMethodsQuery request, CancellationToken ct) => _providers.ListMethodsAsync(ct);
}

// ── Cash on delivery: the money is collected when the goods are delivered ──────

public class CollectCashOnDeliveryCommand : IRequest<bool>
{
    public Guid OrderId { get; set; }
    /// <summary>The delivered parcel (idempotency key of its part of the cash). Empty = a message from before R43: the order id is used.</summary>
    public Guid ShipmentId { get; set; }
    /// <summary>The cash of this parcel (Shipment.CodAmount). 0 = not known (a message from before R43, or a parcel with no cash).</summary>
    public decimal Amount { get; set; }
    /// <summary>Parcels of the order still on the way after this one.</summary>
    public int OtherParcelsPending { get; set; }
}

/// <summary>
/// Captures the cash of one delivered parcel of a cash-on-delivery order (ROADMAP R43): the parcel's own cash, never more than what is left of the
/// payment. A message that does not know the parcel's cash (before R43) collects what is left when it is the last parcel. Idempotent per parcel:
/// a redelivered event, a parcel already paid, or an order that was not COD changes nothing (returns false).
/// </summary>
public class CollectCashOnDeliveryCommandHandler : IRequestHandler<CollectCashOnDeliveryCommand, bool>
{
    private readonly IPaymentUnitOfWork _uow;
    public CollectCashOnDeliveryCommandHandler(IPaymentUnitOfWork uow) => _uow = uow;

    public async Task<bool> Handle(CollectCashOnDeliveryCommand request, CancellationToken ct)
    {
        var payment = await _uow.Payments.GetByOrderIdAsync(request.OrderId, ct);
        if (payment == null || !payment.AcceptsCodCapture) return false;

        var remaining = payment.Amount - payment.CapturedAmount;
        var part = request.Amount > 0 ? Math.Min(request.Amount, remaining) : request.OtherParcelsPending == 0 ? remaining : 0m;
        if (part <= 0) return false;

        var shipmentId = request.ShipmentId != Guid.Empty ? request.ShipmentId : request.OrderId;
        if (payment.CapturePart(shipmentId, part) == null) return false;
        await _uow.Payments.UpdateAsync(payment, ct);
        await _uow.SaveChangesAsync(ct);
        return true;
    }
}

// ── An order cancelled by hand closes its pending payment (risk R18) ───────────

public class CancelPendingPaymentForOrderCommand : IRequest<bool>
{
    public Guid OrderId { get; set; }
}

/// <summary>
/// Sales publishes SalesOrderCancelled for every cancellation (the saga's own path already closes the payment through
/// RefundPaymentCommand). Only a payment that collected nothing (Pending / Authorized) is closed, so a late gateway
/// notification is flagged for refund instead of captured. A Captured payment is NEVER refunded here — money
/// back to the customer stays a deliberate action. Idempotent: a repeat, or an order without a payment, returns false.
/// </summary>
public class CancelPendingPaymentForOrderCommandHandler : IRequestHandler<CancelPendingPaymentForOrderCommand, bool>
{
    private readonly IPaymentUnitOfWork _uow;
    public CancelPendingPaymentForOrderCommandHandler(IPaymentUnitOfWork uow) => _uow = uow;

    public async Task<bool> Handle(CancelPendingPaymentForOrderCommand request, CancellationToken ct)
    {
        var payment = await _uow.Payments.GetByOrderIdAsync(request.OrderId, ct);
        if (payment == null || payment.Status is not (PaymentStatus.Pending or PaymentStatus.Authorized)) return false;

        payment.Cancel();
        await _uow.Payments.UpdateAsync(payment, ct);
        await _uow.SaveChangesAsync(ct);
        return true;
    }
}
