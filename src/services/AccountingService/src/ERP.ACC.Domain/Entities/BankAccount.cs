using ERP.ACC.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    public class BankAccount : AggregateRoot
    {
        public string AccountName { get; private set; } = string.Empty;
        public string BankName { get; private set; } = string.Empty;
        public string AccountNumber { get; private set; } = string.Empty;
        public string CurrencyCode { get; private set; } = "VND";
        public decimal CurrentBalance { get; private set; }
        public bool IsActive { get; private set; }
        public Guid? GlAccountId { get; private set; }

        private BankAccount() { }

        public static BankAccount Create(
            string accountName,
            string bankName,
            string accountNumber,
            decimal openingBalance = 0,
            string currencyCode = "VND",
            Guid? glAccountId = null)
        {
            var ba = new BankAccount
            {
                Id = Guid.NewGuid(),
                AccountName = accountName,
                BankName = bankName,
                AccountNumber = accountNumber,
                CurrencyCode = currencyCode,
                CurrentBalance = openingBalance,
                IsActive = true,
                GlAccountId = glAccountId,
                CreatedAt = DateTime.UtcNow
            };
            ba.AddDomainEvent(new BankAccountCreatedEvent(ba.Id, accountName, bankName, accountNumber));
            return ba;
        }

        public void UpdateBalance(decimal newBalance)
        {
            CurrentBalance = newBalance;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Deactivate()
        {
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
