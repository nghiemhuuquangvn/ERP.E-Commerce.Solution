using ERP.RPT.Application.DTOs;
using ERP.RPT.Domain.Models;

namespace ERP.RPT.Application.Interfaces;

public interface IReportingRepository
{
    Task<SalesDashboardDto> GetSalesDashboardAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<PaymentSummaryDto> GetPaymentSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<InventoryReportDto> GetInventoryReportAsync(CancellationToken ct = default);
    Task<HRDashboardDto> GetHRDashboardAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<POSDashboardDto> GetPOSDashboardAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<FactSalesOrder>> GetSalesOrdersForExportAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<FactPayment>> GetPaymentsForExportAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task UpsertFactSalesOrderAsync(FactSalesOrder fact, CancellationToken ct = default);
    Task UpsertFactPaymentAsync(FactPayment fact, CancellationToken ct = default);
    Task UpsertFactInventoryMovementAsync(FactInventoryMovement fact, CancellationToken ct = default);
    Task UpsertFactPOSTransactionAsync(FactPOSTransaction fact, CancellationToken ct = default);
    Task UpsertFactHRPayslipAsync(FactHRPayslip fact, CancellationToken ct = default);
    Task UpsertDimProductAsync(DimProduct dim, CancellationToken ct = default);
}
