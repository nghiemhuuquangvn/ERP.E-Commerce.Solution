namespace ERP.ACC.Application.DTOs
{
    public class AccountDto
    {
        public Guid Id { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public string NormalBalance { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid? ParentAccountId { get; set; }
        public int Level { get; set; } = 1;
        public bool IsControl { get; set; }

        /// <summary>First digit of the number: the class of the Vietnamese chart (0 when the number does not start with a digit).</summary>
        public int AccountClass { get; set; }

        /// <summary>Active and without active sub-accounts: the only kind of account that takes postings.</summary>
        public bool IsPostable { get; set; } = true;

        /// <summary>Cash or bank account: tracked by the cash-flow statement.</summary>
        public bool IsCash { get; set; }

        /// <summary>Operating | Investing | Financing: how a cash movement against this account is classified (the default applies when none was set).</summary>
        public string CashFlowCategory { get; set; } = "Operating";
    }

    public class AccountTreeNodeDto
    {
        public AccountDto Account { get; set; } = new();
        public List<AccountTreeNodeDto> Children { get; set; } = new();
    }

    public class VatLineDto
    {
        /// <summary>"Taxable" or "NotTaxable".</summary>
        public string Category { get; set; } = "Taxable";
        public decimal RatePercent { get; set; }
        public decimal Net { get; set; }
        public decimal Tax { get; set; }
        public decimal Gross { get; set; }
        public string? Description { get; set; }
    }

    public class InvoiceDto
    {
        public Guid Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }

        /// <summary>Amount before VAT, the VAT rate (percent) and the VAT inside Amount (the gross).</summary>
        public decimal NetAmount { get; set; }
        public decimal TaxRatePercent { get; set; }
        public decimal TaxAmount { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        /// <summary>False when the entry of this invoice could not be posted (no open fiscal period, incomplete posting profile); see the posting gaps (ROADMAP P1-3b).</summary>
        public bool PostedToLedger { get; set; } = true;
        public string? PostingWarning { get; set; }
    }

    public class BillDto
    {
        public Guid Id { get; set; }
        public string BillNumber { get; set; } = string.Empty;
        public Guid SupplierId { get; set; }
        public Guid POId { get; set; }
        public decimal Amount { get; set; }

        /// <summary>Amount before VAT, the VAT rate (percent) and the input VAT inside Amount (the gross).</summary>
        public decimal NetAmount { get; set; }
        public decimal TaxRatePercent { get; set; }
        public decimal TaxAmount { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        /// <summary>False when the entry of the approval could not be posted (ROADMAP P1-3b).</summary>
        public bool PostedToLedger { get; set; } = true;
        public string? PostingWarning { get; set; }
    }

    public class JournalLineDto
    {
        public Guid AccountId { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class JournalEntryDto
    {
        public Guid Id { get; set; }
        public string EntryNumber { get; set; } = string.Empty;
        public Guid FiscalPeriodId { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime EntryDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ReferenceType { get; set; }
        public Guid? ReferenceId { get; set; }
        public string? VoidReason { get; set; }
        public List<JournalLineDto> Lines { get; set; } = new();
    }

    public class PostingMappingDto
    {
        public string PostingKey { get; set; } = string.Empty;
        public Guid AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
    }

    public class PostingProfileDto
    {
        public List<PostingMappingDto> Mappings { get; set; } = new();
    }

    public class PostingMappingInput
    {
        public string PostingKey { get; set; } = string.Empty;
        public Guid AccountId { get; set; }
    }

    public class FiscalPeriodDto
    {
        public Guid Id { get; set; }
        public string PeriodName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ARAgingDto
    {
        public Guid CustomerId { get; set; }
        public decimal Current { get; set; }
        public decimal Days30 { get; set; }
        public decimal Days60 { get; set; }
        public decimal Days90Plus { get; set; }
        public decimal TotalOutstanding { get; set; }
    }

    public class APAgingDto
    {
        public Guid SupplierId { get; set; }
        public decimal Current { get; set; }
        public decimal Days30 { get; set; }
        public decimal Days60 { get; set; }
        public decimal Days90Plus { get; set; }
        public decimal TotalOutstanding { get; set; }
    }

    public class AccountBalanceSummary
    {
        public Guid AccountId { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
    }

    public class FinancialAccountLineDto
    {
        public Guid AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class TrialBalanceLineDto
    {
        public Guid AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public string NormalBalance { get; set; } = string.Empty;
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal Balance { get; set; }

        // Vietnamese trial balance (ROADMAP P1-2b): opening and closing balances as a debit or a credit amount, plus the period's movement.
        public int Level { get; set; } = 1;
        public Guid? ParentAccountId { get; set; }

        /// <summary>The account has sub-accounts; with roll-up its figures are the sum of theirs.</summary>
        public bool IsParent { get; set; }
        public decimal OpeningDebit { get; set; }
        public decimal OpeningCredit { get; set; }
        public decimal PeriodDebit { get; set; }
        public decimal PeriodCredit { get; set; }
        public decimal ClosingDebit { get; set; }
        public decimal ClosingCredit { get; set; }
    }

    public class TrialBalanceTotalsDto
    {
        public decimal OpeningDebit { get; set; }
        public decimal OpeningCredit { get; set; }
        public decimal PeriodDebit { get; set; }
        public decimal PeriodCredit { get; set; }
        public decimal ClosingDebit { get; set; }
        public decimal ClosingCredit { get; set; }

        /// <summary>Debits equal credits in the opening, period and closing columns.</summary>
        public bool IsBalanced { get; set; }
    }

    public class TrialBalanceDto
    {
        public List<TrialBalanceLineDto> Lines { get; set; } = new();
        public TrialBalanceTotalsDto Totals { get; set; } = new();
    }

    /// <summary>One journal line as the ledger reads it (repository projection).</summary>
    public class LedgerRow
    {
        public Guid EntryId { get; set; }
        public string EntryNumber { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; }
        public string EntryDescription { get; set; } = string.Empty;
        public string LineDescription { get; set; } = string.Empty;
        public string? ReferenceType { get; set; }
        public int Status { get; set; }
        public Guid AccountId { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    public record EntryAccountRef(Guid EntryId, Guid AccountId);

    public class LedgerLineDto
    {
        public Guid EntryId { get; set; }
        public string EntryNumber { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; }
        public string Description { get; set; } = string.Empty;

        /// <summary>The account of this line (differs from the ledger's account for a parent that includes sub-accounts).</summary>
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>The other accounts of the entry, e.g. "131, 3331".</summary>
        public string CounterAccounts { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }

        /// <summary>Running balance on the account's normal side after this line.</summary>
        public decimal Balance { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsClosingEntry { get; set; }
    }

    public class AccountLedgerDto
    {
        public Guid AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string NormalBalance { get; set; } = string.Empty;
        public bool IncludesSubAccounts { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal ClosingBalance { get; set; }
        public int TotalRows { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public List<LedgerLineDto> Lines { get; set; } = new();
    }

    /// <summary>One journal line of an entry that touches a cash account (repository projection for the cash-flow statement).</summary>
    public class CashEntryLine
    {
        public Guid EntryId { get; set; }
        public Guid AccountId { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    public class CashFlowLineDto
    {
        public Guid AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Inflow { get; set; }
        public decimal Outflow { get; set; }
        public decimal Net { get; set; }
    }

    public class CashFlowSectionDto
    {
        public string Category { get; set; } = string.Empty;
        public List<CashFlowLineDto> Lines { get; set; } = new();
        public decimal TotalInflow { get; set; }
        public decimal TotalOutflow { get; set; }
        public decimal Net { get; set; }
    }

    /// <summary>Cash-flow statement, direct method, classified by the counter-account of each cash movement.</summary>
    public class CashFlowStatementDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int CashAccountCount { get; set; }
        public decimal OpeningCash { get; set; }
        public CashFlowSectionDto Operating { get; set; } = new() { Category = "Operating" };
        public CashFlowSectionDto Investing { get; set; } = new() { Category = "Investing" };
        public CashFlowSectionDto Financing { get; set; } = new() { Category = "Financing" };
        public decimal NetChange { get; set; }
        public decimal ClosingCash { get; set; }

        /// <summary>Opening cash + net change - closing cash; 0 when the statement agrees with the balance sheet.</summary>
        public decimal Difference { get; set; }
        public bool IsReconciled { get; set; }
    }

    public class ProfitAndLossDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetIncome { get; set; }
        public List<FinancialAccountLineDto> RevenueLines { get; set; } = new();
        public List<FinancialAccountLineDto> ExpenseLines { get; set; } = new();
    }

    public class BalanceSheetDto
    {
        public DateTime AsOfDate { get; set; }
        public decimal TotalAssets { get; set; }
        public decimal TotalLiabilities { get; set; }
        public decimal TotalEquity { get; set; }
        public List<FinancialAccountLineDto> AssetLines { get; set; } = new();
        public List<FinancialAccountLineDto> LiabilityLines { get; set; } = new();
        public List<FinancialAccountLineDto> EquityLines { get; set; } = new();
    }

    public class TaxRateDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public bool IsActive { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class BankAccountDto
    {
        public Guid Id { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; }
        public bool IsActive { get; set; }
        public Guid? GlAccountId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class BankStatementDto
    {
        public Guid Id { get; set; }
        public Guid BankAccountId { get; set; }
        public DateTime StatementDate { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? ReconciledAt { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
