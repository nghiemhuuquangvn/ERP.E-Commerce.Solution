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

        /// <summary>The account holds cash or bank money: the cash-flow statement tracks its movements.</summary>
        public bool IsCash { get; set; }

        /// <summary>Category of a cash movement whose counter-account this is; null = the default (<see cref="EffectiveCashFlowCategory"/>).</summary>
        public int? CashFlowCategory { get; set; }

        /// <summary>Operating unless set; equity accounts (capital, funds, retained earnings) are financing.</summary>
        public Enums.CashFlowCategory EffectiveCashFlowCategory =>
            CashFlowCategory.HasValue && Enum.IsDefined(typeof(Enums.CashFlowCategory), CashFlowCategory.Value)
                ? (Enums.CashFlowCategory)CashFlowCategory.Value
                : AccountType == (int)Enums.AccountType.Equity ? Enums.CashFlowCategory.Financing : Enums.CashFlowCategory.Operating;

        /// <summary>Sets the cash-flow attributes; a null category goes back to the default.</summary>
        public void SetCashFlow(bool isCash, int? category)
        {
            if (category.HasValue && !Enum.IsDefined(typeof(Enums.CashFlowCategory), category.Value))
                throw new ArgumentOutOfRangeException(nameof(category), "The cash-flow category is 1 (operating), 2 (investing) or 3 (financing).");
            IsCash = isCash;
            CashFlowCategory = category;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// The Vietnamese chart's class = the first digit of the number (1-2 assets, 3 liabilities, 4 equity, 5-8 revenue / expense, 9 profit
        /// determination); 0 when the number does not start with a digit. Derived, never stored.
        /// </summary>
        public int AccountClass => AccountNumber.Length > 0 && char.IsDigit(AccountNumber[0]) ? AccountNumber[0] - '0' : 0;

        /// <param name="parent">A child has the same type as its parent and sits one level below it.</param>
        public static Account Create(string accountNumber, string name, int accountType, int normalBalance, Account? parent = null)
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

            if (parent != null)
                account.AttachTo(parent);

            return account;
        }

        public void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("An account needs a name.", nameof(name));
            Name = name.Trim();
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>Puts the account under <paramref name="parent"/> (same type); the level follows the parent.</summary>
        public void AttachTo(Account parent)
        {
            if (parent.Id == Id)
                throw new InvalidOperationException("An account cannot be its own parent.");
            if (parent.AccountType != AccountType)
                throw new InvalidOperationException(
                    $"Account {AccountNumber} cannot sit under {parent.AccountNumber}: a child has the same account type as its parent.");

            ParentAccountId = parent.Id;
            Level = parent.Level + 1;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MakeTopLevel()
        {
            ParentAccountId = null;
            Level = 1;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Activate()
        {
            IsActive = true;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Deactivate()
        {
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Walks the parent chain of <paramref name="candidateParent"/>: true when <paramref name="account"/> is among its ancestors (or is it),
        /// i.e. moving the account under the candidate would create a cycle.
        /// </summary>
        public static bool WouldCreateCycle(Account account, Account candidateParent, IReadOnlyDictionary<Guid, Account> all)
        {
            var current = candidateParent;
            var guard = 0;
            while (current != null && guard++ <= all.Count)
            {
                if (current.Id == account.Id)
                    return true;
                current = current.ParentAccountId.HasValue && all.TryGetValue(current.ParentAccountId.Value, out var next) ? next : null;
            }
            return false;
        }

        /// <summary>Recomputes the level of every account from the parent links (after a move).</summary>
        public static void RecalculateLevels(IReadOnlyCollection<Account> accounts)
        {
            var byId = accounts.ToDictionary(a => a.Id);
            foreach (var account in accounts)
            {
                var level = 1;
                var current = account;
                var guard = 0;
                while (current.ParentAccountId.HasValue && byId.TryGetValue(current.ParentAccountId.Value, out var parent) && guard++ <= accounts.Count)
                {
                    level++;
                    current = parent;
                }
                if (account.Level != level)
                {
                    account.Level = level;
                    account.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}
