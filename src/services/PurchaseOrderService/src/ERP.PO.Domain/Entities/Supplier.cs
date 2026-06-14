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
    public bool IsActive { get; private set; } = true;

    private Supplier() { }

    public static Supplier Create(
        string code,
        string name,
        Guid tenantId,
        string? contactPerson = null,
        string? email = null,
        string? phone = null,
        Address? address = null)
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
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? contactPerson, string? email, string? phone, Address? address)
    {
        Name = name;
        ContactPerson = contactPerson;
        Email = email;
        Phone = phone;
        Address = address;
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
