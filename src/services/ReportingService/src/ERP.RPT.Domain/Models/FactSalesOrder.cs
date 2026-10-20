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
    public string Status { get; set; } = string.Empty;
    public DateTime? ProcessedAt { get; set; }
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
    public decimal TotalAmount { get; set; }
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
