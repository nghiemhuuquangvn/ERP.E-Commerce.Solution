namespace ERP.PO.Application.DTOs;

public class PurchaseOrderDto
{
    public Guid Id { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public SupplierDto? Supplier { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Notes { get; set; }
    public ICollection<PurchaseOrderItemDto> Items { get; set; } = new List<PurchaseOrderItemDto>();
}

public class PurchaseOrderItemDto
{
    public Guid Id { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CreatePurchaseOrderDto
{
    public string PONumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Notes { get; set; }
}

public class AddPurchaseOrderItemDto
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
}

public class UpdatePurchaseOrderItemDto
{
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
