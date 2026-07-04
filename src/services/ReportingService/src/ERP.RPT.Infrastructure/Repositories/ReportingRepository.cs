using ERP.RPT.Application.DTOs;
using ERP.RPT.Application.Interfaces;
using ERP.RPT.Domain.Models;
using ERP.RPT.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.RPT.Infrastructure.Repositories;

public class ReportingRepository : IReportingRepository
{
    private readonly ReportingDbContext _db;
    public ReportingRepository(ReportingDbContext db) => _db = db;

    public async Task<SalesDashboardDto> GetSalesDashboardAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var orders = await _db.FactSalesOrders
            .Where(o => o.OrderDate >= from && o.OrderDate <= to)
            .AsNoTracking().ToListAsync(ct);

        var revenueByDay = orders
            .GroupBy(o => o.OrderDate.Date)
            .Select(g => new RevenueByDayDto
            {
                Date = g.Key,
                Revenue = g.Sum(x => x.TotalAmount),
                OrderCount = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToList();

        return new SalesDashboardDto
        {
            TotalRevenue = orders.Sum(o => o.TotalAmount),
            TotalOrders = orders.Count,
            AverageOrderValue = orders.Count > 0 ? orders.Average(o => o.TotalAmount) : 0,
            NewCustomers = orders.Select(o => o.CustomerId).Distinct().Count(),
            RevenueByDay = revenueByDay
        };
    }

    public async Task<PaymentSummaryDto> GetPaymentSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var payments = await _db.FactPayments
            .Where(p => p.ProcessedAt >= from && p.ProcessedAt <= to)
            .AsNoTracking().ToListAsync(ct);

        var byMethod = payments
            .GroupBy(p => p.Method)
            .Select(g => new PaymentByMethodDto
            {
                Method = g.Key,
                Count = g.Count(),
                Amount = g.Sum(x => x.Amount)
            }).ToList();

        return new PaymentSummaryDto
        {
            TotalProcessed = payments.Where(p => p.Status == "Processed").Sum(p => p.Amount),
            TotalRefunded = payments.Where(p => p.Status == "Refunded").Sum(p => p.Amount),
            ByMethod = byMethod
        };
    }

    public async Task<InventoryReportDto> GetInventoryReportAsync(CancellationToken ct = default)
    {
        var movements = await _db.FactInventoryMovements.AsNoTracking().ToListAsync(ct);
        var products = await _db.DimProducts.AsNoTracking()
            .ToDictionaryAsync(p => p.ProductId, p => p.Name, ct);

        var stockByProduct = movements
            .GroupBy(m => m.ProductId)
            .Select(g =>
            {
                var netQty = g.Sum(m =>
                {
                    var t = m.MovementType.ToLowerInvariant();
                    if (t.Contains("receipt") || t.Contains("receive") || t == "1")
                        return m.Quantity;
                    if (t.Contains("shipment") || t.Contains("ship") || t.Contains("issue") || t == "2")
                        return -m.Quantity;
                    // adjustment or transfer: treat as signed quantity as-is
                    return m.Quantity;
                });
                var name = products.TryGetValue(g.Key, out var n) ? n : g.Key.ToString()[..8];
                return new { ProductId = g.Key, NetQty = netQty, Name = name };
            })
            .ToList();

        var levels = stockByProduct
            .Select(s => new StockLevelDto
            {
                ProductId = s.ProductId,
                ProductName = s.Name,
                AvailableQuantity = s.NetQty,
                StockStatus = s.NetQty <= 0 ? "OutOfStock" : s.NetQty < 10 ? "LowStock" : "InStock"
            })
            .OrderBy(s => s.AvailableQuantity)
            .ToList();

        return new InventoryReportDto
        {
            TotalProducts = levels.Count,
            LowStockCount = levels.Count(s => s.StockStatus == "LowStock"),
            OutOfStockCount = levels.Count(s => s.StockStatus == "OutOfStock"),
            StockLevels = levels
        };
    }

    public async Task<HRDashboardDto> GetHRDashboardAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var payslips = await _db.FactHRPayslips
            .Where(p => (!p.PaidAt.HasValue || (p.PaidAt >= from && p.PaidAt <= to)))
            .AsNoTracking().ToListAsync(ct);

        var byPeriod = payslips
            .GroupBy(p => p.Period)
            .OrderBy(g => g.Key)
            .Select(g => new PayrollByPeriodDto
            {
                Period = g.Key,
                EmployeeCount = g.Select(p => p.EmployeeId).Distinct().Count(),
                TotalAmount = g.Sum(p => p.TotalAmount)
            })
            .ToList();

        return new HRDashboardDto
        {
            TotalPayslips = payslips.Count,
            TotalPayroll = payslips.Sum(p => p.TotalAmount),
            AverageSalary = payslips.Count > 0 ? payslips.Average(p => p.TotalAmount) : 0,
            ByPeriod = byPeriod
        };
    }

    public async Task<POSDashboardDto> GetPOSDashboardAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var txns = await _db.FactPOSTransactions
            .Where(t => t.OccurredAt >= from && t.OccurredAt <= to)
            .AsNoTracking().ToListAsync(ct);

        var byDay = txns
            .GroupBy(t => t.OccurredAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new RevenueByDayDto
            {
                Date = g.Key,
                Revenue = g.Sum(t => t.TotalAmount),
                OrderCount = g.Count()
            })
            .ToList();

        return new POSDashboardDto
        {
            TotalTransactions = txns.Count,
            TotalRevenue = txns.Sum(t => t.TotalAmount),
            AverageTransactionValue = txns.Count > 0 ? txns.Average(t => t.TotalAmount) : 0,
            RevenueByDay = byDay
        };
    }

    public async Task<List<FactSalesOrder>> GetSalesOrdersForExportAsync(DateTime from, DateTime to, CancellationToken ct = default) =>
        await _db.FactSalesOrders
            .Where(o => o.OrderDate >= from && o.OrderDate <= to)
            .AsNoTracking().OrderBy(o => o.OrderDate).ToListAsync(ct);

    public async Task<List<FactPayment>> GetPaymentsForExportAsync(DateTime from, DateTime to, CancellationToken ct = default) =>
        await _db.FactPayments
            .Where(p => p.ProcessedAt >= from && p.ProcessedAt <= to)
            .AsNoTracking().OrderBy(p => p.ProcessedAt).ToListAsync(ct);

    public async Task UpsertFactSalesOrderAsync(FactSalesOrder fact, CancellationToken ct = default)
    {
        var existing = await _db.FactSalesOrders.FindAsync(new object[] { fact.OrderId }, ct);
        if (existing == null)
            await _db.FactSalesOrders.AddAsync(fact, ct);
        else
            _db.Entry(existing).CurrentValues.SetValues(fact);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpsertFactPaymentAsync(FactPayment fact, CancellationToken ct = default)
    {
        var existing = await _db.FactPayments.FindAsync(new object[] { fact.PaymentId }, ct);
        if (existing == null)
            await _db.FactPayments.AddAsync(fact, ct);
        else
            _db.Entry(existing).CurrentValues.SetValues(fact);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpsertFactInventoryMovementAsync(FactInventoryMovement fact, CancellationToken ct = default)
    {
        var existing = await _db.FactInventoryMovements.FindAsync(new object[] { fact.MovementId }, ct);
        if (existing == null)
            await _db.FactInventoryMovements.AddAsync(fact, ct);
        else
            _db.Entry(existing).CurrentValues.SetValues(fact);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpsertFactPOSTransactionAsync(FactPOSTransaction fact, CancellationToken ct = default)
    {
        var existing = await _db.FactPOSTransactions.FindAsync(new object[] { fact.TransactionId }, ct);
        if (existing == null)
            await _db.FactPOSTransactions.AddAsync(fact, ct);
        else
            _db.Entry(existing).CurrentValues.SetValues(fact);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpsertFactHRPayslipAsync(FactHRPayslip fact, CancellationToken ct = default)
    {
        var existing = await _db.FactHRPayslips.FindAsync(new object[] { fact.PayslipId }, ct);
        if (existing == null)
            await _db.FactHRPayslips.AddAsync(fact, ct);
        else
            _db.Entry(existing).CurrentValues.SetValues(fact);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpsertDimProductAsync(DimProduct dim, CancellationToken ct = default)
    {
        var existing = await _db.DimProducts.FindAsync(new object[] { dim.ProductId }, ct);
        if (existing == null)
            await _db.DimProducts.AddAsync(dim, ct);
        else
            _db.Entry(existing).CurrentValues.SetValues(dim);
        await _db.SaveChangesAsync(ct);
    }
}
