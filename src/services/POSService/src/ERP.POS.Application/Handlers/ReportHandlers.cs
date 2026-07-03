using ERP.POS.Application.DTOs;
using ERP.POS.Application.Interfaces;
using ERP.POS.Application.Queries;
using MediatR;

namespace ERP.POS.Application.Handlers;

/// <summary>
/// The end-of-day "Z-report" — aggregates every shift (and its transactions) opened
/// on the given date, optionally scoped to one terminal. Distinct from
/// GetShiftSummaryQuery, which is scoped to a single shift.
/// </summary>
public class GetDailyZReportQueryHandler : IRequestHandler<GetDailyZReportQuery, DailyZReportDto>
{
    private readonly ICashShiftRepository _shifts;
    private readonly ITransactionRepository _transactions;

    public GetDailyZReportQueryHandler(ICashShiftRepository shifts, ITransactionRepository transactions)
    {
        _shifts = shifts;
        _transactions = transactions;
    }

    public async Task<DailyZReportDto> Handle(GetDailyZReportQuery request, CancellationToken cancellationToken)
    {
        var start = request.Date.Date;
        var end = start.AddDays(1).AddTicks(-1);

        var shifts = await _shifts.GetByDateRangeAsync(request.TerminalId, start, end, cancellationToken);

        var report = new DailyZReportDto
        {
            Date = start,
            TerminalId = request.TerminalId,
            ShiftCount = shifts.Count
        };

        foreach (var shift in shifts)
        {
            var transactions = await _transactions.GetByShiftAsync(shift.Id, cancellationToken);
            var summary = GetShiftSummaryQueryHandler.BuildSummary(shift.Id, transactions);

            report.Shifts.Add(summary);
            report.CompletedTransactionCount += summary.CompletedTransactionCount;
            report.VoidedTransactionCount += summary.VoidedTransactionCount;
            report.TotalSales += summary.TotalSales;
            report.TotalDiscount += summary.TotalDiscount;
            report.TotalTax += summary.TotalTax;

            foreach (var (method, amount) in summary.SalesByPaymentMethod)
                report.SalesByPaymentMethod[method] = report.SalesByPaymentMethod.GetValueOrDefault(method) + amount;
        }

        return report;
    }
}

/// <summary>
/// Backs a customer-facing secondary display — polls the terminal's currently open
/// transaction for a running total/line-item view. No hardware integration exists
/// in this repo to push updates to a physical display; this is the data source a
/// customer-display client (a browser tab, a small screen app) would poll or
/// subscribe to.
/// </summary>
public class GetCustomerDisplayQueryHandler : IRequestHandler<GetCustomerDisplayQuery, CustomerDisplayDto?>
{
    private readonly ITransactionRepository _transactions;
    public GetCustomerDisplayQueryHandler(ITransactionRepository transactions) => _transactions = transactions;

    public async Task<CustomerDisplayDto?> Handle(GetCustomerDisplayQuery request, CancellationToken cancellationToken)
    {
        var transaction = await _transactions.GetOpenByTerminalAsync(request.TerminalId, cancellationToken);
        if (transaction == null)
            return null;

        return new CustomerDisplayDto
        {
            TransactionId = transaction.Id,
            Items = transaction.Items.Select(i => new TransactionItemDto
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TaxRate = i.TaxRate,
                DiscountAmount = i.DiscountAmount,
                LineTotal = i.LineTotal,
                TaxAmount = i.TaxAmount
            }).ToList(),
            SubTotal = transaction.SubTotal,
            DiscountAmount = transaction.DiscountAmount,
            TaxAmount = transaction.TaxAmount,
            TotalAmount = transaction.TotalAmount
        };
    }
}
