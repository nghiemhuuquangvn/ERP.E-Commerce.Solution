namespace ERP.PAY.Application.DTOs;

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid? InvoiceId { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Cash collected so far (less than Amount while a COD order's parcels are still on the way).</summary>
    public decimal CapturedAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? TransactionRef { get; set; }
    public string? GatewayTransactionId { get; set; }
    public string? GatewayProvider { get; set; }
    /// <summary>The customer's choice at checkout (COD, VNPay, MoMo, ZaloPay).</summary>
    public string? Provider { get; set; }
    public Guid? CustomerId { get; set; }
    public string? OrderNumber { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? AuthorizedAt { get; set; }
    public DateTime? CapturedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public bool IsFlaggedForReview { get; set; }
    public string? FraudReviewReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<RefundDto> Refunds { get; set; } = new();
}

public class RefundDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? GatewayRefundId { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class SavedPaymentMethodDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Last4 { get; set; }
    public int? ExpiryMonth { get; set; }
    public int? ExpiryYear { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class InstallmentLineDto
{
    public Guid Id { get; set; }
    public int SequenceNo { get; set; }
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
}

public class InstallmentPlanDto
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public int TotalInstallments { get; set; }
    public decimal TotalAmount { get; set; }
    public List<InstallmentLineDto> Installments { get; set; } = new();
}

public class ReconciliationReportDto
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int CapturedPaymentCount { get; set; }
    public decimal CapturedTotal { get; set; }
    public int SettledPaymentCount { get; set; }
    public decimal SettledTotal { get; set; }
    public int UnsettledPaymentCount { get; set; }
    public decimal UnsettledTotal { get; set; }
    public List<Guid> UnsettledPaymentIds { get; set; } = new();
}

public class SettlementDto
{
    public Guid Id { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal TotalAmount { get; set; }
    public int TransactionCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? SettledAt { get; set; }
}
