using ERP.PAY.Application.DTOs;
using MediatR;

namespace ERP.PAY.Application.Queries;

public class ListPaymentsQuery : IRequest<ListPaymentsResult>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
}

public class ListPaymentsResult
{
    public List<PaymentDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

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
