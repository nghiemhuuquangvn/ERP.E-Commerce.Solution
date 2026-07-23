using ERP.PO.Domain.ValueObjects;
using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Entities;

public class Supplier : BaseEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? ContactPerson { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public Address? Address { get; private set; }
    public string? TaxCode { get; private set; }

    /// <summary>
    /// Fallback net-days value used when <see cref="PaymentTermId"/> isn't set — the
    /// SQL column has carried this since 001_CreatePurchasingDB.sql, this was just
    /// never exposed on the domain entity until now.
    /// </summary>
    public int PaymentTermDays { get; private set; } = 30;

    /// <summary>
    /// Normalized reference to SharedMasterDataService's PaymentTerm by Id only
    /// (cross-service, no database FK). Null means <see cref="PaymentTermDays"/>
    /// applies as a plain net-days fallback.
    /// </summary>
    public Guid? PaymentTermId { get; private set; }
    public string CurrencyCode { get; private set; } = "USD";
    public bool IsActive { get; private set; } = true;

    private Supplier() { }

    public static Supplier Create(
        string code,
        string name,
        Guid tenantId,
        string? contactPerson = null,
        string? email = null,
        string? phone = null,
        Address? address = null,
        string? taxCode = null,
        int paymentTermDays = 30,
        string currencyCode = "USD")
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Supplier Code is required", nameof(code));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Supplier Name is required", nameof(name));

        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required", nameof(tenantId));

        return new Supplier
        {
            Code = code,
            Name = name,
            TenantId = tenantId,
            ContactPerson = contactPerson,
            Email = email,
            Phone = phone,
            Address = address,
            TaxCode = taxCode,
            PaymentTermDays = paymentTermDays,
            CurrencyCode = currencyCode,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string name,
        string? contactPerson,
        string? email,
        string? phone,
        Address? address,
        string? taxCode = null,
        string? currencyCode = null)
    {
        Name = name;
        ContactPerson = contactPerson;
        Email = email;
        Phone = phone;
        Address = address;
        TaxCode = taxCode;
        if (currencyCode is not null)
            CurrencyCode = currencyCode;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPaymentTerm(Guid? paymentTermId, int paymentTermDays)
    {
        PaymentTermId = paymentTermId;
        PaymentTermDays = paymentTermDays;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
