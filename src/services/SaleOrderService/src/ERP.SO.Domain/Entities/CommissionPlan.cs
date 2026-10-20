using ERP.Shared.Core.Domain;

namespace ERP.SO.Domain.Entities
{
    public enum CommissionRateType
    {
        Percent = 0
        // Tiered is a bigger rule-engine problem — add only when a real tiered use case shows up.
    }

    public class CommissionPlan : ITenantOwned
    {
        public Guid Id { get; private set; }
        /// <summary>Owning tenant — stamped by SalesOrderDbContext on insert (EnforceTenantOnSave).</summary>
        public Guid TenantId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public CommissionRateType RateType { get; private set; }
        /// <summary>Fraction of order total, e.g. 0.05 = 5%.</summary>
        public decimal RateValue { get; private set; }
        public bool IsActive { get; private set; }

        protected CommissionPlan() { }

        public static CommissionPlan Create(string name, decimal rateValue)
        {
            if (rateValue <= 0)
                throw new ArgumentException("Rate value must be greater than 0", nameof(rateValue));

            return new CommissionPlan
            {
                Id = Guid.NewGuid(),
                Name = name,
                RateType = CommissionRateType.Percent,
                RateValue = rateValue,
                IsActive = true
            };
        }

        public void Deactivate() => IsActive = false;
    }
}
