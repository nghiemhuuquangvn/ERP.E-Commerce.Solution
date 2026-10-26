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
        /// <summary>VAT paid on purchases and deductible (Vas99 133): debited by an approved bill (ROADMAP P1-3). On the 14-account chart it shares the tax account with VatOutput.</summary>
        public const string VatInput = "VatInput";
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
        /// <summary>Revenue given back for returned goods (contra-revenue). Defaults to the revenue account; a VAS chart gives it its own.</summary>
        public const string SalesReturns = "SalesReturns";
        /// <summary>Temporary account the period-end closing moves revenue and expenses into (VAS 911). The simple chart has none and uses retained earnings.</summary>
        public const string IncomeSummary = "IncomeSummary";
        /// <summary>Where the result of a closed period lands (VAS 421).</summary>
        public const string RetainedEarnings = "RetainedEarnings";
        /// <summary>Borrowings owed to lenders: credited when a loan is drawn, debited by the principal of each repayment (ROADMAP P1-2j).</summary>
        public const string LoanPayable = "LoanPayable";
        /// <summary>Interest paid on loans, debited by the interest part of each repayment (no accrual before it is paid).</summary>
        public const string InterestExpense = "InterestExpense";
        /// <summary>Interest accrued on loans and not yet paid: credited by an accrual, debited by the repayment that pays it (ROADMAP P1-2k).</summary>
        public const string InterestPayable = "InterestPayable";

        /// <summary>Gain / loss when a foreign document is settled at another rate than it was booked at, and the unrealized one of the period-end revaluation (ROADMAP P1-4).</summary>
        public const string FxGain = "FxGain";
        public const string FxLoss = "FxLoss";

        /// <summary>Charges the bank takes (statement lines posted from the reconciliation, ROADMAP P1-4) and the interest it pays on deposits.</summary>
        public const string BankFee = "BankFee";
        public const string BankInterestIncome = "BankInterestIncome";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Cash, Bank, AccountsReceivable, AccountsPayable, SalesRevenue, Inventory, VatOutput,
            GoodsReceivedNotInvoiced, CostOfGoodsSold, InventoryAdjustment, CashOverShort,
            SalaryExpense, SalariesPayable, PayrollWithholdingsPayable,
            SalesReturns, IncomeSummary, RetainedEarnings, LoanPayable, InterestExpense, InterestPayable, VatInput, FxGain, FxLoss, BankFee, BankInterestIncome
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
            [VatInput] = "2200",                   // one tax account on the simple chart: input VAT nets against output VAT
            [GoodsReceivedNotInvoiced] = "2100",   // Accrued Liabilities
            [CostOfGoodsSold] = "5000",
            [InventoryAdjustment] = "5000",
            [CashOverShort] = "5100",              // Operating Expenses
            [SalaryExpense] = "5200",              // Salaries & Benefits
            [SalariesPayable] = "2100",            // Accrued Liabilities
            [PayrollWithholdingsPayable] = "2100", // VAS (P1-2) splits insurance and personal income tax
            [SalesReturns] = "4000",               // the default chart has no contra-revenue account
            [IncomeSummary] = "3100",              // the simple chart closes straight into retained earnings
            [RetainedEarnings] = "3100",
            [LoanPayable] = "2100",                // the simple chart has no borrowings account; Accrued Liabilities holds them
            [InterestExpense] = "5100",            // Operating Expenses
            [InterestPayable] = "2100",            // Accrued Liabilities
            [FxGain] = "4000",                     // the simple chart has no financial-income account
            [FxLoss] = "5100",                     // Operating Expenses
            [BankFee] = "5100",                    // Operating Expenses
            [BankInterestIncome] = "4000",         // the simple chart has no financial-income account
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
