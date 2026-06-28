namespace ERP.RPT.Application.DTOs;

public class SalesDashboardDto
{
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int NewCustomers { get; set; }
    public List<RevenueByDayDto> RevenueByDay { get; set; } = new();
    public List<TopProductDto> TopProducts { get; set; } = new();
}

public class RevenueByDayDto
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
    public int OrderCount { get; set; }
}

public class TopProductDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
}

public class PaymentSummaryDto
{
    public decimal TotalProcessed { get; set; }
    public decimal TotalRefunded { get; set; }
    public List<PaymentByMethodDto> ByMethod { get; set; } = new();
}

public class PaymentByMethodDto
{
    public string Method { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public class InventoryReportDto
{
    public int TotalProducts { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
    public List<StockLevelDto> StockLevels { get; set; } = new();
}

public class StockLevelDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public string StockStatus { get; set; } = string.Empty;
}
