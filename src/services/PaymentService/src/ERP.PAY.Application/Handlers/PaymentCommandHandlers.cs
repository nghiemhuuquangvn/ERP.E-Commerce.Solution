using ERP.PAY.Application.Commands;
using ERP.PAY.Application.DTOs;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Domain.Entities;
using MediatR;

namespace ERP.PAY.Application.Handlers;

public class InitiatePaymentCommandHandler : IRequestHandler<InitiatePaymentCommand, PaymentDto>
{
    private readonly IPaymentUnitOfWork _uow;
    private readonly IFraudDetectionService _fraudDetection;

    public InitiatePaymentCommandHandler(IPaymentUnitOfWork uow, IFraudDetectionService fraudDetection)
    {
        _uow = uow;
        _fraudDetection = fraudDetection;
    }

    public async Task<PaymentDto> Handle(InitiatePaymentCommand request, CancellationToken cancellationToken)
    {
        // Payment.OrderId is unique per payment — a redelivered SalesOrderCreated
        // event (Kafka/RabbitMQ at-least-once delivery) must not crash on a DB unique
        // constraint violation, so treat re-initiation for an already-known order as
        // idempotent instead of attempting a second insert.
        var existing = await _uow.Payments.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (existing != null)
            return MapToDto(existing);

        var provider = ERP.PAY.Domain.Enums.PaymentProviders.Normalize(request.Provider);
        var method = provider != null ? ERP.PAY.Domain.Enums.PaymentProviders.MethodFor(provider) : request.Method;
        var payment = Payment.Initiate(request.OrderId, request.Amount, method, request.Currency, request.InvoiceId,
            provider, request.CustomerId, request.OrderNumber);

        var fraudCheck = await _fraudDetection.EvaluateAsync(request.OrderId, request.Amount, cancellationToken);
        if (fraudCheck.IsSuspicious)
            payment.FlagForReview(fraudCheck.Reason!);

        await _uow.Payments.AddAsync(payment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return MapToDto(payment);
    }

    internal static PaymentDto MapToDto(Payment p) => new()
    {
        Id = p.Id,
        OrderId = p.OrderId,
        InvoiceId = p.InvoiceId,
        Amount = p.Amount,
        Currency = p.Currency,
        Method = p.Method.ToString(),
        Status = p.Status.ToString(),
        TransactionRef = p.TransactionRef,
        GatewayTransactionId = p.GatewayTransactionId,
        GatewayProvider = p.GatewayProvider,
        Provider = p.Provider,
        CustomerId = p.CustomerId,
        OrderNumber = p.OrderNumber,
        FailureReason = p.FailureReason,
        AuthorizedAt = p.AuthorizedAt,
        CapturedAt = p.CapturedAt,
        CancelledAt = p.CancelledAt,
        IsFlaggedForReview = p.IsFlaggedForReview,
        FraudReviewReason = p.FraudReviewReason,
        CreatedAt = p.CreatedAt,
        Refunds = p.Refunds.Select(r => new RefundDto
        {
            Id = r.Id,
            Amount = r.Amount,
            Reason = r.Reason,
            Status = r.Status.ToString(),
            GatewayRefundId = r.GatewayRefundId,
            FailureReason = r.FailureReason,
            CompletedAt = r.CompletedAt
        }).ToList()
    };
}

public class AssignTransactionRefCommandHandler : IRequestHandler<AssignTransactionRefCommand, PaymentDto>
{
    private readonly IPaymentUnitOfWork _uow;
    public AssignTransactionRefCommandHandler(IPaymentUnitOfWork uow) => _uow = uow;

    public async Task<PaymentDto> Handle(AssignTransactionRefCommand request, CancellationToken cancellationToken)
    {
        var payment = await _uow.Payments.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {request.PaymentId} not found.");

        payment.AssignTransactionRef(request.TransactionRef);
        await _uow.Payments.UpdateAsync(payment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return InitiatePaymentCommandHandler.MapToDto(payment);
    }
}

public class LinkInvoiceCommandHandler : IRequestHandler<LinkInvoiceCommand, PaymentDto>
{
    private readonly IPaymentUnitOfWork _uow;
    public LinkInvoiceCommandHandler(IPaymentUnitOfWork uow) => _uow = uow;

    public async Task<PaymentDto> Handle(LinkInvoiceCommand request, CancellationToken cancellationToken)
    {
        var payment = await _uow.Payments.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {request.PaymentId} not found.");

        payment.LinkInvoice(request.InvoiceId);
        await _uow.Payments.UpdateAsync(payment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return InitiatePaymentCommandHandler.MapToDto(payment);
    }
}

public class ProcessGatewayCallbackCommandHandler : IRequestHandler<ProcessGatewayCallbackCommand, PaymentDto>
{
    private readonly IPaymentUnitOfWork _uow;
    public ProcessGatewayCallbackCommandHandler(IPaymentUnitOfWork uow) => _uow = uow;

    public async Task<PaymentDto> Handle(ProcessGatewayCallbackCommand request, CancellationToken cancellationToken)
    {
        var payment = await _uow.Payments.GetByTransactionRefAsync(request.TransactionRef, cancellationToken)
            ?? throw new InvalidOperationException($"No payment found for transaction reference '{request.TransactionRef}'.");

        // Gateways retry callbacks/IPNs — a payment already resolved to a terminal
        // state must not be re-processed (Authorize/Capture would throw since they
        // require the Pending/Authorized starting state).
        if (payment.Status is Domain.Enums.PaymentStatus.Captured or Domain.Enums.PaymentStatus.Failed)
            return InitiatePaymentCommandHandler.MapToDto(payment);

        if (request.IsPaid)
        {
            // What the gateway says was paid must be what the order costs; otherwise nothing is captured and a person looks at it.
            if (request.Amount.HasValue && decimal.Round(request.Amount.Value, 0) != decimal.Round(payment.Amount, 0))
            {
                payment.FlagForReview($"Gateway reported {request.Amount.Value:0.##} paid for a payment of {payment.Amount:0.##}");
                await _uow.Payments.UpdateAsync(payment, cancellationToken);
                await _uow.SaveChangesAsync(cancellationToken);
                throw new InvalidOperationException("The paid amount does not match the payment.");
            }
            payment.Authorize(request.GatewayTransactionId ?? string.Empty, request.GatewayProvider ?? "VNPay");
            payment.Capture();
        }
        else
        {
            payment.Fail(request.FailureReason ?? "Gateway reported payment failure");
        }

        await _uow.Payments.UpdateAsync(payment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return InitiatePaymentCommandHandler.MapToDto(payment);
    }
}

public class AuthorizePaymentCommandHandler : IRequestHandler<AuthorizePaymentCommand, PaymentDto>
{
    private readonly IPaymentUnitOfWork _uow;
    public AuthorizePaymentCommandHandler(IPaymentUnitOfWork uow) => _uow = uow;

    public async Task<PaymentDto> Handle(AuthorizePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _uow.Payments.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {request.PaymentId} not found.");

        payment.Authorize(request.GatewayTransactionId, request.GatewayProvider);
        await _uow.Payments.UpdateAsync(payment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return InitiatePaymentCommandHandler.MapToDto(payment);
    }
}

public class CapturePaymentCommandHandler : IRequestHandler<CapturePaymentCommand, PaymentDto>
{
    private readonly IPaymentUnitOfWork _uow;
    public CapturePaymentCommandHandler(IPaymentUnitOfWork uow) => _uow = uow;

    public async Task<PaymentDto> Handle(CapturePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _uow.Payments.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {request.PaymentId} not found.");

        payment.Capture();
        await _uow.Payments.UpdateAsync(payment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return InitiatePaymentCommandHandler.MapToDto(payment);
    }
}

public class RefundPaymentCommandHandler : IRequestHandler<RefundPaymentCommand, PaymentDto>
{
    private readonly IPaymentUnitOfWork _uow;
    private readonly IPaymentProviderResolver _providers;

    public RefundPaymentCommandHandler(IPaymentUnitOfWork uow, IPaymentProviderResolver providers)
    {
        _uow = uow;
        _providers = providers;
    }

    public async Task<PaymentDto> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _uow.Payments.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {request.PaymentId} not found.");

        var refund = payment.AddRefund(request.Amount, request.Reason);

        // Previously the refund only ever flipped Payment.Status and the Refund row
        // stayed Pending forever — Refund.MarkAsCompleted/MarkAsFailed existed but no
        // call site ever invoked them, and no money was ever actually returned via the
        // gateway. Now the gateway is genuinely called before the refund is
        // considered final.
        var transactionRef = payment.TransactionRef ?? payment.Id.ToString("N")[..16].ToUpperInvariant();
        GatewayRefundResult gatewayResult;
        try
        {
            // A payment that went through a gateway is refunded at THAT gateway with the tenant's keys; one captured by hand has none.
            if (_providers.StubFor(payment.GatewayProvider) is { } stub)
                gatewayResult = await stub.RefundAsync(new GatewayCredentials("Null", string.Empty, null, null, null, true),
                    payment.GatewayTransactionId ?? string.Empty, transactionRef, request.Amount, request.Reason, cancellationToken);
            else
            {
                var resolved = await _providers.ResolveAsync(payment.GatewayProvider!, cancellationToken);
                gatewayResult = await resolved.Provider.RefundAsync(resolved.Credentials,
                    payment.GatewayTransactionId ?? string.Empty, transactionRef, request.Amount, request.Reason, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            gatewayResult = new GatewayRefundResult(false, null, ex.Message);
        }

        // The refund event (and with it the ledger entry) exists only for a refund the gateway confirmed.
        if (gatewayResult.Success)
            payment.CompleteRefund(refund, gatewayResult.GatewayRefundId ?? string.Empty);
        else if (gatewayResult.Pending)
        {
            // The gateway accepted it and is still processing: not failed, not yet money back (no status poll yet — DEFERRED).
            refund.MarkAsProcessing(gatewayResult.GatewayRefundId ?? string.Empty);
            payment.ReconcileStatusWithConfirmedRefunds();
        }
        else
        {
            refund.MarkAsFailed(gatewayResult.Error ?? "Gateway refund failed");
            payment.ReconcileStatusWithConfirmedRefunds();
        }

        await _uow.Payments.UpdateAsync(payment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return InitiatePaymentCommandHandler.MapToDto(payment);
    }
}

public class CancelPaymentCommandHandler : IRequestHandler<CancelPaymentCommand, PaymentDto>
{
    private readonly IPaymentUnitOfWork _uow;
    public CancelPaymentCommandHandler(IPaymentUnitOfWork uow) => _uow = uow;

    public async Task<PaymentDto> Handle(CancelPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _uow.Payments.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {request.PaymentId} not found.");

        payment.Cancel();
        await _uow.Payments.UpdateAsync(payment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return InitiatePaymentCommandHandler.MapToDto(payment);
    }
}

public class ProcessSettlementCommandHandler : IRequestHandler<ProcessSettlementCommand, SettlementDto>
{
    private readonly IPaymentUnitOfWork _uow;

    public ProcessSettlementCommandHandler(IPaymentUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<SettlementDto> Handle(ProcessSettlementCommand request, CancellationToken cancellationToken)
    {
        var capturedPayments = await _uow.Payments.GetByStatusAsync(Domain.Enums.PaymentStatus.Captured, cancellationToken);
        var inPeriod = capturedPayments
            .Where(p => p.CapturedAt >= request.PeriodStart && p.CapturedAt <= request.PeriodEnd)
            .ToList();

        var settlement = Settlement.Create(
            request.PeriodStart,
            request.PeriodEnd,
            inPeriod.Sum(p => p.Amount),
            inPeriod.Count);

        settlement.Process();
        settlement.Complete();

        await _uow.Settlements.AddAsync(settlement, cancellationToken);

        // Previously a Settlement was just a sum with no record of which specific
        // payments it covered, making reconciliation against it impossible beyond
        // date-range guessing. Linking each included payment closes that gap.
        foreach (var payment in inPeriod)
        {
            payment.AssignSettlement(settlement.Id);
            await _uow.Payments.UpdateAsync(payment, cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);

        return new SettlementDto
        {
            Id = settlement.Id,
            PeriodStart = settlement.PeriodStart,
            PeriodEnd = settlement.PeriodEnd,
            TotalAmount = settlement.TotalAmount,
            TransactionCount = settlement.TransactionCount,
            Status = settlement.Status.ToString(),
            SettledAt = settlement.SettledAt
        };
    }
}
