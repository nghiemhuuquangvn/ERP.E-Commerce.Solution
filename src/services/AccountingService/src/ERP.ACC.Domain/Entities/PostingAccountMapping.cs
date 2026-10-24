using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    /// <summary>
    /// Posting keys: what a generated journal line stands for. Business flows post "Dr AccountsReceivable / Cr SalesRevenue"
    /// and the tenant's <see cref="PostingAccountMapping"/> decides which account of its chart that is, so moving to another
    /// chart (the Vietnamese VAS one, ROADMAP P1-2) means editing the mapping, not the code.
    /// </summary>
    public static class PostingKeys
    {
        public const string Cash = "Cash";
        public const string Bank = "Bank";
        public const string AccountsReceivable = "AccountsReceivable";
        public const string AccountsPayable = "AccountsPayable";
        public const string SalesRevenue = "SalesRevenue";
        public const string Inventory = "Inventory";
        public const string VatOutput = "VatOutput";
        /// <summary>Goods received, not yet invoiced: credited by a stock receipt, debited by the supplier bill (ROADMAP P1-1b).</summary>
        public const string GoodsReceivedNotInvoiced = "GoodsReceivedNotInvoiced";
        public const string CostOfGoodsSold = "CostOfGoodsSold";
        /// <summary>Stock gains and losses (stocktake, damage, POS void). Its own key so VAS can give it a separate account.</summary>
        public const string InventoryAdjustment = "InventoryAdjustment";
        /// <summary>Counted cash that differs from the expected cash of a POS shift (ROADMAP P1-1c).</summary>
        public const string CashOverShort = "CashOverShort";
        /// <summary>Wages and overtime of the pay slips (ROADMAP P1-1d).</summary>
        public const string SalaryExpense = "SalaryExpense";
        /// <summary>Net pay owed to employees until it is paid out.</summary>
        public const string SalariesPayable = "SalariesPayable";
        /// <summary>Deductions withheld from pay (insurance, tax, other): HRM sends one lump until real payroll (P1-7) splits it.</summary>
        public const string PayrollWithholdingsPayable = "PayrollWithholdingsPayable";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Cash, Bank, AccountsReceivable, AccountsPayable, SalesRevenue, Inventory, VatOutput,
            GoodsReceivedNotInvoiced, CostOfGoodsSold, InventoryAdjustment, CashOverShort,
            SalaryExpense, SalariesPayable, PayrollWithholdingsPayable
        };

        /// <summary>Account numbers of the seeded 14-account chart, used when a tenant has no mapping yet.</summary>
        public static readonly IReadOnlyDictionary<string, string> DefaultAccountNumbers = new Dictionary<string, string>
        {
            [Cash] = "1000",
            [Bank] = "1000",   // the seeded chart has no separate bank account
            [AccountsReceivable] = "1100",
            [AccountsPayable] = "2000",
            [SalesRevenue] = "4000",
            [Inventory] = "1200",
            [VatOutput] = "2200",
            [GoodsReceivedNotInvoiced] = "2100",   // Accrued Liabilities
            [CostOfGoodsSold] = "5000",
            [InventoryAdjustment] = "5000",
            [CashOverShort] = "5100",              // Operating Expenses
            [SalaryExpense] = "5200",              // Salaries & Benefits
            [SalariesPayable] = "2100",            // Accrued Liabilities
            [PayrollWithholdingsPayable] = "2100", // VAS (P1-2) splits insurance and personal income tax
        };
    }

    /// <summary>Which account a posting key uses for one tenant.</summary>
    public class PostingAccountMapping : AggregateRoot
    {
        public string PostingKey { get; private set; } = string.Empty;
        public Guid AccountId { get; private set; }

        private PostingAccountMapping() { }

        public static PostingAccountMapping Create(Guid tenantId, string postingKey, Guid accountId)
        {
            if (!PostingKeys.All.Contains(postingKey))
                throw new ArgumentException($"Unknown posting key '{postingKey}'.", nameof(postingKey));
            if (accountId == Guid.Empty)
                throw new ArgumentException("AccountId is required", nameof(accountId));

            return new PostingAccountMapping
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PostingKey = postingKey,
                AccountId = accountId,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void PointTo(Guid accountId)
        {
            if (accountId == Guid.Empty)
                throw new ArgumentException("AccountId is required", nameof(accountId));
            AccountId = accountId;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
