namespace ERP.PO.Application.DTOs;

public class SupplierDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public AddressDto? Address { get; set; }
    public string? TaxCode { get; set; }
    public int PaymentTermDays { get; set; }
    public Guid? PaymentTermId { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public bool IsActive { get; set; }
}

public class CreateSupplierDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public AddressDto? Address { get; set; }
    public string? TaxCode { get; set; }
    public int PaymentTermDays { get; set; } = 30;
    public string CurrencyCode { get; set; } = "USD";
}

public class UpdateSupplierDto
{
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public AddressDto? Address { get; set; }
    public string? TaxCode { get; set; }
    public string? CurrencyCode { get; set; }
}

/// <summary>
/// Request body for the dedicated "set payment term" action. PaymentTermId references
/// SharedMasterDataService's PaymentTerm by Id only (cross-service, no DB FK); null means
/// PaymentTermDays applies as a plain net-days fallback.
/// </summary>
public class SetSupplierPaymentTermDto
{
    public Guid? PaymentTermId { get; set; }
    public int PaymentTermDays { get; set; } = 30;
}

public class AddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

/// <summary>
/// What a Supplier can supply, at what reference price/lead time. ProductId/VariantId
/// reference ProductService's Product/ProductVariant by Id only (cross-service, no
/// database FK).
/// </summary>
public class SupplierItemDto
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string? SupplierItemCode { get; set; }
    public decimal LastPurchasePrice { get; set; }
    public int LeadTimeDays { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateSupplierItemDto
{
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string? SupplierItemCode { get; set; }
    public decimal LastPurchasePrice { get; set; }
    public int LeadTimeDays { get; set; }
}

public class UpdateSupplierItemDto
{
    public decimal LastPurchasePrice { get; set; }
    public int LeadTimeDays { get; set; }
    public string? SupplierItemCode { get; set; }
}
