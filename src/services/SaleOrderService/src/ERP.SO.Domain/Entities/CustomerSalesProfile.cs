namespace ERP.SO.Domain.Entities
{
    /// <summary>
    /// Sales-owned credit concept for a customer — genuinely new state Sales must maintain
    /// itself, since no other service in this system owns a CreditLimit field (CRMService
    /// has no Customer entity, IdentityService's User has none either). Unlike
    /// HRM's EmployeeSnapshot, there is no external source of truth to mirror here.
    /// </summary>
    public class CustomerSalesProfile
    {
        public Guid Id { get; private set; }
        public Guid CustomerId { get; private set; }
        public decimal CreditLimit { get; private set; }
        /// <summary>Denormalized running total of open (Approved..Delivered, not yet Cancelled) order value — not a lifetime total.</summary>
        public decimal CurrentBalance { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        protected CustomerSalesProfile() { }

        public static CustomerSalesProfile Create(Guid customerId, decimal creditLimit)
        {
            return new CustomerSalesProfile
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                CreditLimit = creditLimit,
                CurrentBalance = 0,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public void SetCreditLimit(decimal creditLimit)
        {
            if (creditLimit < 0)
                throw new ArgumentException("Credit limit cannot be negative", nameof(creditLimit));

            CreditLimit = creditLimit;
            UpdatedAt = DateTime.UtcNow;
        }

        public void IncreaseExposure(decimal amount)
        {
            CurrentBalance += amount;
            UpdatedAt = DateTime.UtcNow;
        }

        public void DecreaseExposure(decimal amount)
        {
            CurrentBalance = Math.Max(0, CurrentBalance - amount);
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
