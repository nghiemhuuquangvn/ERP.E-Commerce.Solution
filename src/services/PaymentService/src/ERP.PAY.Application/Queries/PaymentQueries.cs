using ERP.PAY.Application.DTOs;
using MediatR;

namespace ERP.PAY.Application.Queries;

public class GetPaymentQuery : IRequest<PaymentDto?>
{
    public Guid PaymentId { get; set; }
}

public class GetPaymentByOrderQuery : IRequest<PaymentDto?>
{
    public Guid OrderId { get; set; }
}

public class ListSettlementsQuery : IRequest<List<SettlementDto>> { }

public class GetReconciliationReportQuery : IRequest<ReconciliationReportDto>
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
}
