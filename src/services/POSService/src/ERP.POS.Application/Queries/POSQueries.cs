using ERP.POS.Application.DTOs;
using MediatR;

namespace ERP.POS.Application.Queries;

public class GetTransactionQuery : IRequest<TransactionDto?>
{
    public Guid TransactionId { get; set; }
}

public class GetShiftSummaryQuery : IRequest<ShiftSummaryDto?>
{
    public Guid ShiftId { get; set; }
}

public class GetActiveShiftQuery : IRequest<CashShiftDto?>
{
    public Guid TerminalId { get; set; }
}

public class GetProductByBarcodeQuery : IRequest<ProductLookupDto?>
{
    public string Barcode { get; set; } = string.Empty;
}

public class GetDailyZReportQuery : IRequest<DailyZReportDto>
{
    public Guid? TerminalId { get; set; }
    public DateTime Date { get; set; }
}

public class GetCustomerDisplayQuery : IRequest<CustomerDisplayDto?>
{
    public Guid TerminalId { get; set; }
}

public class GetReceiptQuery : IRequest<string>
{
    public Guid TransactionId { get; set; }
}

public class ListTerminalsQuery : IRequest<List<TerminalDto>>
{
    public bool? IsActive { get; set; }
}

public class GetTerminalQuery : IRequest<TerminalDto?>
{
    public Guid TerminalId { get; set; }
}

/// <summary>Back-office search of transactions. Status is the enum name (Open / Completed / Voided / Suspended); From / To are inclusive days.</summary>
public class ListTransactionsQuery : IRequest<PosPagedResult<TransactionDto>>
{
    public Guid? TerminalId { get; set; }
    public Guid? CashierId { get; set; }
    public string? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>Back-office search of cash shifts. Status is the enum name (Open / Closed).</summary>
public class ListShiftsQuery : IRequest<PosPagedResult<CashShiftDto>>
{
    public Guid? TerminalId { get; set; }
    public string? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
