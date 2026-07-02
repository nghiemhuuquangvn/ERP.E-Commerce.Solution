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
