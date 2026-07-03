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
