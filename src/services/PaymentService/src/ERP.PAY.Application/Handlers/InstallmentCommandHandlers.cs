using ERP.PAY.Application.Commands;
using ERP.PAY.Application.DTOs;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Domain.Entities;
using MediatR;

namespace ERP.PAY.Application.Handlers;

public class CreateInstallmentPlanCommandHandler : IRequestHandler<CreateInstallmentPlanCommand, InstallmentPlanDto>
{
    private readonly IInstallmentPlanRepository _plans;
    private readonly IPaymentRepository _payments;

    public CreateInstallmentPlanCommandHandler(IInstallmentPlanRepository plans, IPaymentRepository payments)
    {
        _plans = plans;
        _payments = payments;
    }

    public async Task<InstallmentPlanDto> Handle(CreateInstallmentPlanCommand request, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {request.PaymentId} not found.");

        var existing = await _plans.GetByPaymentIdAsync(request.PaymentId, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException($"Payment {request.PaymentId} already has an installment plan.");

        var plan = InstallmentPlan.Create(payment.Id, payment.Amount, request.TotalInstallments, request.FirstDueDate, request.IntervalDays);
        await _plans.AddAsync(plan, cancellationToken);

        return MapToDto(plan);
    }

    internal static InstallmentPlanDto MapToDto(InstallmentPlan p) => new()
    {
        Id = p.Id,
        PaymentId = p.PaymentId,
        TotalInstallments = p.TotalInstallments,
        TotalAmount = p.TotalAmount,
        Installments = p.Installments.Select(i => new InstallmentLineDto
        {
            Id = i.Id,
            SequenceNo = i.SequenceNo,
            Amount = i.Amount,
            DueDate = i.DueDate,
            Status = i.Status.ToString(),
            PaidAt = i.PaidAt
        }).OrderBy(i => i.SequenceNo).ToList()
    };
}

public class RecordInstallmentPaymentCommandHandler : IRequestHandler<RecordInstallmentPaymentCommand, InstallmentPlanDto>
{
    private readonly IInstallmentPlanRepository _plans;
    public RecordInstallmentPaymentCommandHandler(IInstallmentPlanRepository plans) => _plans = plans;

    public async Task<InstallmentPlanDto> Handle(RecordInstallmentPaymentCommand request, CancellationToken cancellationToken)
    {
        var plan = await _plans.GetByIdAsync(request.InstallmentPlanId, cancellationToken)
            ?? throw new InvalidOperationException($"Installment plan {request.InstallmentPlanId} not found.");

        plan.RecordInstallmentPayment(request.SequenceNo);
        await _plans.UpdateAsync(plan, cancellationToken);

        return CreateInstallmentPlanCommandHandler.MapToDto(plan);
    }
}
