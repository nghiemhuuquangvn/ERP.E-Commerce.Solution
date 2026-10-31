using ERP.Shared.Core.Domain;

namespace ERP.RPT.Domain.Models;

public class FactSalesOrder : ITenantOwned
{
    public Guid OrderId { get; set; }
    /// <summary>Owning tenant — stamped by ReportingDbContext on insert from the source event's TenantScope.</summary>
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public int DateKey { get; set; }
}

public class FactPayment : ITenantOwned
{
    public Guid PaymentId { get; set; }
    /// <summary>Owning tenant — stamped by ReportingDbContext on insert from the source event's TenantScope.</summary>
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    /// <summary>Processed, Failed, PartiallyRefunded (some of the amount came back) or Refunded (all of it).</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Sum of the payment's refunds (<see cref="FactPaymentRefund"/>), so a partial refund is not reported as a full one (ROADMAP P1-6).</summary>
    public decimal RefundedAmount { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int DateKey { get; set; }
}

/// <summary>One part of a payment collected on delivery (ROADMAP R43): its id (the parcel's shipment) makes a replayed event count once.</summary>
public class FactPaymentCapture : ITenantOwned
{
    public Guid CaptureId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public DateTime? CapturedAt { get; set; }
}

/// <summary>One refund of a payment; its id makes a replayed event count once.</summary>
public class FactPaymentRefund : ITenantOwned
{
    public Guid RefundId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public DateTime RefundedAt { get; set; }
}

/// <summary>An opportunity CRM marked as won (ROADMAP P1-6): count and value of the deals won per day.</summary>
public class FactCrmOpportunity : ITenantOwned
{
    public Guid OpportunityId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ContactId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime WonAt { get; set; }
    public int DateKey { get; set; }
}

public class FactInventoryMovement : ITenantOwned
{
    public Guid MovementId { get; set; }
    /// <summary>Owning tenant — stamped by ReportingDbContext on insert from the source event's TenantScope.</summary>
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime OccurredAt { get; set; }
    public int DateKey { get; set; }
}

public class DimDate
{
    public int DateKey { get; set; }
    public DateTime Date { get; set; }
    public int Year { get; set; }
    public int Quarter { get; set; }
    public int Month { get; set; }
    public int Week { get; set; }
    public int Day { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public string DayName { get; set; } = string.Empty;
    public bool IsWeekend { get; set; }
}

public class FactPOSTransaction : ITenantOwned
{
    public Guid TransactionId { get; set; }
    /// <summary>Owning tenant — stamped by ReportingDbContext on insert from the source event's TenantScope.</summary>
    public Guid TenantId { get; set; }
    public Guid StoreId { get; set; }
    public Guid CashierId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public int ItemCount { get; set; }
    public DateTime OccurredAt { get; set; }
    public int DateKey { get; set; }
}

public class FactHRPayslip : ITenantOwned
{
    public Guid PayslipId { get; set; }
    /// <summary>Owning tenant — stamped by ReportingDbContext on insert from the source event's TenantScope.</summary>
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public decimal BasicSalary { get; set; }
    public decimal OvertimePay { get; set; }
    /// <summary>The net pay (what the employee receives).</summary>
    public decimal TotalAmount { get; set; }
    /// <summary>Gross pay before insurance and tax, the withholdings, the income tax inside them, and the employer's insurance on top (ROADMAP P1-7 / P1-6).</summary>
    public decimal GrossSalary { get; set; }
    public decimal Deductions { get; set; }
    public decimal IncomeTax { get; set; }
    public decimal EmployerInsurance { get; set; }
    public DateTime? PaidAt { get; set; }
    public int DateKey { get; set; }
}

public class DimProduct : ITenantOwned
{
    public Guid ProductId { get; set; }
    /// <summary>Owning tenant — stamped by ReportingDbContext on insert from the source event's TenantScope.</summary>
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public int StockQuantity { get; set; }
}
