using ERP.RPT.Application.DTOs;
using ERP.RPT.Domain.Models;

namespace ERP.RPT.Application.Interfaces;

public interface IReportingRepository
{
    Task<SalesDashboardDto> GetSalesDashboardAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<PaymentSummaryDto> GetPaymentSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<InventoryReportDto> GetInventoryReportAsync(CancellationToken ct = default);
    Task UpsertFactSalesOrderAsync(FactSalesOrder fact, CancellationToken ct = default);
    Task UpsertFactPaymentAsync(FactPayment fact, CancellationToken ct = default);
    Task UpsertFactInventoryMovementAsync(FactInventoryMovement fact, CancellationToken ct = default);
}
