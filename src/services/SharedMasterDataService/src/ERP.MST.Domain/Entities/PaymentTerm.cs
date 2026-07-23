using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class PaymentTerm : AggregateRoot
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int NetDays { get; set; }
        public decimal? DepositPercent { get; set; }
        public bool IsActive { get; set; } = true;

        public static PaymentTerm Create(
            string code,
            string name,
            int netDays,
            decimal? depositPercent = null)
        {
            return new PaymentTerm
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                NetDays = netDays,
                DepositPercent = depositPercent,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public void Update(string name, int netDays, decimal? depositPercent)
        {
            Name = name;
            NetDays = netDays;
            DepositPercent = depositPercent;
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
}
