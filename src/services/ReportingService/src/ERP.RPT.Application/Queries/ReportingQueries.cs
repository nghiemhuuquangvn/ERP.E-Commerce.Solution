using ERP.RPT.Application.DTOs;
using MediatR;

namespace ERP.RPT.Application.Queries;

public class GetSalesDashboardQuery : IRequest<SalesDashboardDto>
{
    public DateTime From { get; set; } = DateTime.UtcNow.AddDays(-30);
    public DateTime To { get; set; } = DateTime.UtcNow;
}

public class GetPaymentSummaryQuery : IRequest<PaymentSummaryDto>
{
    public DateTime From { get; set; } = DateTime.UtcNow.AddDays(-30);
    public DateTime To { get; set; } = DateTime.UtcNow;
}

public class GetInventoryReportQuery : IRequest<InventoryReportDto> { }

public class GetHRDashboardQuery : IRequest<HRDashboardDto>
{
    public DateTime From { get; set; } = DateTime.UtcNow.AddMonths(-6);
    public DateTime To { get; set; } = DateTime.UtcNow;
}

public class GetPOSDashboardQuery : IRequest<POSDashboardDto>
{
    public DateTime From { get; set; } = DateTime.UtcNow.AddDays(-30);
    public DateTime To { get; set; } = DateTime.UtcNow;
}
