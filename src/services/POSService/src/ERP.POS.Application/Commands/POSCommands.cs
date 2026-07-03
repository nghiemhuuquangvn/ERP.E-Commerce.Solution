using ERP.POS.Application.DTOs;
using MediatR;

namespace ERP.POS.Application.Commands;

public class OpenTransactionCommand : IRequest<TransactionDto>
{
    public Guid TerminalId { get; set; }
    public Guid CashierId { get; set; }
    public Guid? CashShiftId { get; set; }
}

public class AddTransactionItemCommand : IRequest<TransactionDto>
{
    public Guid TransactionId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
}

public class ApplyDiscountCommand : IRequest<TransactionDto>
{
    public Guid TransactionId { get; set; }
    public decimal DiscountAmount { get; set; }
    public Guid? PromotionId { get; set; }
}

public class TenderRequest
{
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class CompleteTransactionCommand : IRequest<TransactionDto>
{
    public Guid TransactionId { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public List<TenderRequest>? Tenders { get; set; }
}

public class VoidTransactionCommand : IRequest<TransactionDto>
{
    public Guid TransactionId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class DenominationCountRequest
{
    public decimal Denomination { get; set; }
    public int Count { get; set; }
}

public class OpenCashShiftCommand : IRequest<CashShiftDto>
{
    public Guid TerminalId { get; set; }
    public Guid CashierId { get; set; }
    public decimal OpeningBalance { get; set; }
    public List<DenominationCountRequest>? OpeningCounts { get; set; }
}

public class CloseCashShiftCommand : IRequest<CashShiftDto>
{
    public Guid ShiftId { get; set; }
    public decimal ClosingBalance { get; set; }
    public string? Notes { get; set; }
    public List<DenominationCountRequest>? ClosingCounts { get; set; }
}
