using ERP.Shared.Core.Domain;

namespace ERP.PAY.Domain.Entities;

/// <summary>
/// Stores only an opaque gateway token (e.g. from a client-side tokenization widget)
/// plus display metadata — raw card numbers are never received or persisted here,
/// consistent with PCI-DSS scope reduction.
/// </summary>
public class SavedPaymentMethod : BaseEntity
{
    public Guid CustomerId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string Token { get; private set; } = string.Empty;
    public string? Brand { get; private set; }
    public string? Last4 { get; private set; }
    public int? ExpiryMonth { get; private set; }
    public int? ExpiryYear { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; } = true;

    private SavedPaymentMethod() { }

    public static SavedPaymentMethod Create(
        Guid customerId, string provider, string token, string? brand, string? last4, int? expiryMonth, int? expiryYear)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Payment method token is required.");

        return new SavedPaymentMethod
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Provider = provider,
            Token = token,
            Brand = brand,
            Last4 = last4,
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            IsDefault = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetAsDefault()
    {
        IsDefault = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearDefault()
    {
        IsDefault = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        IsDefault = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
