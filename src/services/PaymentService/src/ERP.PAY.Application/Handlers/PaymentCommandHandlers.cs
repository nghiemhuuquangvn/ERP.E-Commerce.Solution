using ERP.PAY.Application.Commands;
using ERP.PAY.Application.DTOs;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Domain.Entities;
using MediatR;

namespace ERP.PAY.Application.Handlers;

public class InitiatePaymentCommandHandler : IRequestHandler<InitiatePaymentCommand, PaymentDto>
{
    private readonly IPaymentRepository _payments;
    public InitiatePaymentCommandHandler(IPaymentRepository payments) => _payments = payments;

    public async Task<PaymentDto> Handle(InitiatePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = Payment.Initiate(request.OrderId, request.Amount, request.Method, request.Currency);
        await _payments.AddAsync(payment, cancellationToken);
        return MapToDto(payment);
    }

    internal static PaymentDto MapToDto(Payment p) => new()
    {
        Id = p.Id,
        OrderId = p.OrderId,
        Amount = p.Amount,
        Currency = p.Currency,
        Method = p.Method.ToString(),
        Status = p.Status.ToString(),
        GatewayTransactionId = p.GatewayTransactionId,
        GatewayProvider = p.GatewayProvider,
        FailureReason = p.FailureReason,
        AuthorizedAt = p.AuthorizedAt,
        CapturedAt = p.CapturedAt,
        CreatedAt = p.CreatedAt,
        Refunds = p.Refunds.Select(r => new RefundDto
        {
            Id = r.Id,
            Amount = r.Amount,
            Reason = r.Reason,
            Status = r.Status.ToString(),
            CompletedAt = r.CompletedAt
        }).ToList()
    };
}

public class AuthorizePaymentCommandHandler : IRequestHandler<AuthorizePaymentCommand, PaymentDto>
{
    private readonly IPaymentRepository _payments;
    public AuthorizePaymentCommandHandler(IPaymentRepository payments) => _payments = payments;

    public async Task<PaymentDto> Handle(AuthorizePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {request.PaymentId} not found.");

        payment.Authorize(request.GatewayTransactionId, request.GatewayProvider);
        await _payments.UpdateAsync(payment, cancellationToken);
        return InitiatePaymentCommandHandler.MapToDto(payment);
    }
}

public class CapturePaymentCommandHandler : IRequestHandler<CapturePaymentCommand, PaymentDto>
{
    private readonly IPaymentRepository _payments;
    public CapturePaymentCommandHandler(IPaymentRepository payments) => _payments = payments;

    public async Task<PaymentDto> Handle(CapturePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {request.PaymentId} not found.");

        payment.Capture();
        await _payments.UpdateAsync(payment, cancellationToken);
        return InitiatePaymentCommandHandler.MapToDto(payment);
    }
}

public class RefundPaymentCommandHandler : IRequestHandler<RefundPaymentCommand, PaymentDto>
{
    private readonly IPaymentRepository _payments;
    public RefundPaymentCommandHandler(IPaymentRepository payments) => _payments = payments;

    public async Task<PaymentDto> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {request.PaymentId} not found.");

        payment.AddRefund(request.Amount, request.Reason);
        await _payments.UpdateAsync(payment, cancellationToken);
        return InitiatePaymentCommandHandler.MapToDto(payment);
    }
}

public class CancelPaymentCommandHandler : IRequestHandler<CancelPaymentCommand, PaymentDto>
{
    private readonly IPaymentRepository _payments;
    public CancelPaymentCommandHandler(IPaymentRepository payments) => _payments = payments;

    public async Task<PaymentDto> Handle(CancelPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {request.PaymentId} not found.");

        payment.Cancel();
        await _payments.UpdateAsync(payment, cancellationToken);
        return InitiatePaymentCommandHandler.MapToDto(payment);
    }
}

public class ProcessSettlementCommandHandler : IRequestHandler<ProcessSettlementCommand, SettlementDto>
{
    private readonly IPaymentRepository _payments;
    private readonly ISettlementRepository _settlements;

    public ProcessSettlementCommandHandler(IPaymentRepository payments, ISettlementRepository settlements)
    {
        _payments = payments;
        _settlements = settlements;
    }

    public async Task<SettlementDto> Handle(ProcessSettlementCommand request, CancellationToken cancellationToken)
    {
        var capturedPayments = await _payments.GetByStatusAsync(Domain.Enums.PaymentStatus.Captured, cancellationToken);
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

        await _settlements.AddAsync(settlement, cancellationToken);

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
