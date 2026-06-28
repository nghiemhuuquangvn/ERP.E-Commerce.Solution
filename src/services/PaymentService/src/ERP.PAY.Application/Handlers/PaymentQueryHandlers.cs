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
