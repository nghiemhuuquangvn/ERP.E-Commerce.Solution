using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    public class Account : AggregateRoot
    {
        public string AccountNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int AccountType { get; set; }
        public int NormalBalance { get; set; }

        /// <summary>An inactive account takes no new postings.</summary>
        public bool IsActive { get; set; } = true;
        public Guid? ParentAccountId { get; set; }
        public int Level { get; set; } = 1;

        /// <summary>Control account (receivables, payables, cash): normally posted by the system, not by hand.</summary>
        public bool IsControl { get; set; }

        public static Account Create(string accountNumber, string name, int accountType, int normalBalance)
        {
            var account = new Account
            {
                Id = Guid.NewGuid(),
                AccountNumber = accountNumber,
                Name = name,
                AccountType = accountType,
                NormalBalance = normalBalance,
                CreatedAt = DateTime.UtcNow
            };

            return account;
        }
    }
}
