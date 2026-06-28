namespace ERP.RPT.Domain.Models;

public class FactSalesOrder
{
    public Guid OrderId { get; set; }
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

public class FactPayment
{
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ProcessedAt { get; set; }
    public int DateKey { get; set; }
}

public class FactInventoryMovement
{
    public Guid MovementId { get; set; }
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
