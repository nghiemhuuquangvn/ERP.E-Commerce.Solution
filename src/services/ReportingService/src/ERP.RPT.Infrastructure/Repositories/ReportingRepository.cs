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
            TotalProcessed = payments.Where(p => p.Status == "Captured").Sum(p => p.Amount),
            TotalRefunded = payments.Where(p => p.Status == "Refunded").Sum(p => p.Amount),
            ByMethod = byMethod
        };
    }

    public async Task<InventoryReportDto> GetInventoryReportAsync(CancellationToken ct = default)
    {
        return new InventoryReportDto
        {
            TotalProducts = 0,
            LowStockCount = 0,
            OutOfStockCount = 0,
            StockLevels = new()
        };
    }

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
}
