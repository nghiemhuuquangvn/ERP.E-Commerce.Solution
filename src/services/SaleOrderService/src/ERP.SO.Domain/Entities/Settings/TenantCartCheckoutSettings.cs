using ERP.Shared.Core.Domain;

namespace ERP.SO.Domain.Entities.Settings;

/// <summary>Per-tenant Cart/Checkout/Customer configuration. One row per TenantId (unique).</summary>
public class TenantCartCheckoutSettings : BaseEntity
{
    public int CartHoldMinutes { get; private set; } = 60;
    public bool RequireRegistrationAtCheckout { get; private set; } = false;
    public int CheckoutSteps { get; private set; } = 3;

    protected TenantCartCheckoutSettings() { }

    public static TenantCartCheckoutSettings CreateDefault(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required", nameof(tenantId));

        return new TenantCartCheckoutSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CartHoldMinutes = 60,
            RequireRegistrationAtCheckout = false,
            CheckoutSteps = 3,
            CreatedBy = "System",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(int cartHoldMinutes, bool requireRegistrationAtCheckout, int checkoutSteps)
    {
        CartHoldMinutes = cartHoldMinutes;
        RequireRegistrationAtCheckout = requireRegistrationAtCheckout;
        CheckoutSteps = checkoutSteps;
    }
}
