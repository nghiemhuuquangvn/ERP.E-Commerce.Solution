using ERP.RPT.Application.DTOs;
using ERP.RPT.Application.Interfaces;
using ERP.RPT.Application.Queries;
using MediatR;

namespace ERP.RPT.Application.Handlers;

public class GetSalesDashboardQueryHandler : IRequestHandler<GetSalesDashboardQuery, SalesDashboardDto>
{
    private readonly IReportingRepository _repository;
    public GetSalesDashboardQueryHandler(IReportingRepository repository) => _repository = repository;

    public Task<SalesDashboardDto> Handle(GetSalesDashboardQuery request, CancellationToken cancellationToken) =>
        _repository.GetSalesDashboardAsync(request.From, request.To, cancellationToken);
}

public class GetPaymentSummaryQueryHandler : IRequestHandler<GetPaymentSummaryQuery, PaymentSummaryDto>
{
    private readonly IReportingRepository _repository;
    public GetPaymentSummaryQueryHandler(IReportingRepository repository) => _repository = repository;

    public Task<PaymentSummaryDto> Handle(GetPaymentSummaryQuery request, CancellationToken cancellationToken) =>
        _repository.GetPaymentSummaryAsync(request.From, request.To, cancellationToken);
}

public class GetInventoryReportQueryHandler : IRequestHandler<GetInventoryReportQuery, InventoryReportDto>
{
    private readonly IReportingRepository _repository;
    public GetInventoryReportQueryHandler(IReportingRepository repository) => _repository = repository;

    public Task<InventoryReportDto> Handle(GetInventoryReportQuery request, CancellationToken cancellationToken) =>
        _repository.GetInventoryReportAsync(cancellationToken);
}
