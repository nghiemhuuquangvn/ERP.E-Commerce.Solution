using ERP.PAY.Application.DTOs;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Application.Queries;
using MediatR;

namespace ERP.PAY.Application.Handlers;

public class GetPaymentQueryHandler : IRequestHandler<GetPaymentQuery, PaymentDto?>
{
    private readonly IPaymentRepository _payments;
    public GetPaymentQueryHandler(IPaymentRepository payments) => _payments = payments;

    public async Task<PaymentDto?> Handle(GetPaymentQuery request, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIdAsync(request.PaymentId, cancellationToken);
        return payment == null ? null : InitiatePaymentCommandHandler.MapToDto(payment);
    }
}

public class GetPaymentByOrderQueryHandler : IRequestHandler<GetPaymentByOrderQuery, PaymentDto?>
{
    private readonly IPaymentRepository _payments;
    public GetPaymentByOrderQueryHandler(IPaymentRepository payments) => _payments = payments;

    public async Task<PaymentDto?> Handle(GetPaymentByOrderQuery request, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByOrderIdAsync(request.OrderId, cancellationToken);
        return payment == null ? null : InitiatePaymentCommandHandler.MapToDto(payment);
    }
}

public class GetReconciliationReportQueryHandler : IRequestHandler<GetReconciliationReportQuery, ReconciliationReportDto>
{
    private readonly IPaymentRepository _payments;
    public GetReconciliationReportQueryHandler(IPaymentRepository payments) => _payments = payments;

    public async Task<ReconciliationReportDto> Handle(GetReconciliationReportQuery request, CancellationToken cancellationToken)
    {
        var captured = await _payments.GetByStatusAsync(Domain.Enums.PaymentStatus.Captured, cancellationToken);
        var inPeriod = captured
            .Where(p => p.CapturedAt >= request.PeriodStart && p.CapturedAt <= request.PeriodEnd)
            .ToList();

        var settled = inPeriod.Where(p => p.SettlementId != null).ToList();
        var unsettled = inPeriod.Where(p => p.SettlementId == null).ToList();

        return new ReconciliationReportDto
        {
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            CapturedPaymentCount = inPeriod.Count,
            CapturedTotal = inPeriod.Sum(p => p.Amount),
            SettledPaymentCount = settled.Count,
            SettledTotal = settled.Sum(p => p.Amount),
            UnsettledPaymentCount = unsettled.Count,
            UnsettledTotal = unsettled.Sum(p => p.Amount),
            UnsettledPaymentIds = unsettled.Select(p => p.Id).ToList()
        };
    }
}

public class ListSettlementsQueryHandler : IRequestHandler<ListSettlementsQuery, List<SettlementDto>>
{
    private readonly ISettlementRepository _settlements;
    public ListSettlementsQueryHandler(ISettlementRepository settlements) => _settlements = settlements;

    public async Task<List<SettlementDto>> Handle(ListSettlementsQuery request, CancellationToken cancellationToken)
    {
        var settlements = await _settlements.GetAllAsync(cancellationToken);
        return settlements.Select(s => new SettlementDto
        {
            Id = s.Id,
            PeriodStart = s.PeriodStart,
            PeriodEnd = s.PeriodEnd,
            TotalAmount = s.TotalAmount,
            TransactionCount = s.TransactionCount,
            Status = s.Status.ToString(),
            SettledAt = s.SettledAt
        }).ToList();
    }
}
