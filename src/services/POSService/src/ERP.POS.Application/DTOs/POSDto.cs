namespace ERP.POS.Application.DTOs;

public class TransactionDto
{
    public Guid Id { get; set; }
    public Guid TerminalId { get; set; }
    public Guid CashierId { get; set; }
    public Guid? PromotionId { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? PaymentMethod { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? VoidReason { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? VoidedAt { get; set; }
    public Guid? ExternalPaymentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TransactionItemDto> Items { get; set; } = new();
    public List<TenderDto> Tenders { get; set; } = new();
}

public class TransactionItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal TaxAmount { get; set; }
}

public class TenderDto
{
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class DenominationDto
{
    public decimal Denomination { get; set; }
    public int Count { get; set; }
    public decimal LineTotal { get; set; }
}

public class CashShiftDto
{
    public Guid Id { get; set; }
    public Guid TerminalId { get; set; }
    public Guid CashierId { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal? ClosingBalance { get; set; }
    public decimal TotalSales { get; set; }
    public int TransactionCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public List<DenominationDto> OpeningDenominations { get; set; } = new();
    public List<DenominationDto> ClosingDenominations { get; set; } = new();
}

public class ShiftSummaryDto
{
    public Guid ShiftId { get; set; }
    public int CompletedTransactionCount { get; set; }
    public int VoidedTransactionCount { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalTax { get; set; }
    public Dictionary<string, decimal> SalesByPaymentMethod { get; set; } = new();
}

public class TerminalDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? LocationId { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProductLookupDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
}

public class DailyZReportDto
{
    public DateTime Date { get; set; }
    public Guid? TerminalId { get; set; }
    public int ShiftCount { get; set; }
    public int CompletedTransactionCount { get; set; }
    public int VoidedTransactionCount { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalTax { get; set; }
    public Dictionary<string, decimal> SalesByPaymentMethod { get; set; } = new();
    public List<ShiftSummaryDto> Shifts { get; set; } = new();
}

public class CustomerDisplayDto
{
    public Guid TransactionId { get; set; }
    public List<TransactionItemDto> Items { get; set; } = new();
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
}
