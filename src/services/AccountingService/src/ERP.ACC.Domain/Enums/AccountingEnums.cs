namespace ERP.ACC.Domain.Enums
{
    public enum AccountType
    {
        Asset = 1,
        Liability = 2,
        Equity = 3,
        Revenue = 4,
        Expense = 5,
        /// <summary>Class 0 (accounts 001 – 008): tracked in the off-balance register, never in a journal entry (ROADMAP P1-2i).</summary>
        OffBalance = 6
    }

    /// <summary>How a cash movement is classified in the cash-flow statement, by the counter-account (ROADMAP P1-2b).</summary>
    public enum CashFlowCategory
    {
        Operating = 1,
        Investing = 2,
        Financing = 3
    }

    /// <summary>When a tenant writes its closing entries (ROADMAP P1-2d): every period, or only at the end of the fiscal year.</summary>
    public enum ClosingMode
    {
        Monthly = 1,
        YearEnd = 2
    }

    /// <summary>The unit statements are shown in (ROADMAP P1-2e): VND, thousand VND or million VND.</summary>
    public enum AmountUnit
    {
        Vnd = 1,
        Thousand = 2,
        Million = 3
    }

    /// <summary>Which way an off-balance movement moves the account: In raises its balance, Out lowers it.</summary>
    public enum OffBalanceDirection
    {
        In = 1,
        Out = 2
    }

    public enum OffBalanceStatus
    {
        Posted = 1,
        Voided = 2
    }

    /// <summary>How the interest of a loan is counted (ROADMAP P1-2l): from the scheduled interest split by days (the original rule), or from the outstanding principal and the annual rate over a 365- or 360-day year.</summary>
    public enum LoanDayCount
    {
        Schedule = 0,
        Actual365 = 1,
        Actual360 = 2
    }

    public enum LoanStatus
    {
        Active = 1,
        Repaid = 2,
        Cancelled = 3
    }

    public enum NormalBalance
    {
        Debit = 1,
        Credit = 2
    }

    public enum InvoiceStatus
    {
        Unpaid = 0,
        Paid = 1,
        Overdue = 2,
        /// <summary>Void (5 in the SQL comment): the order was cancelled before the invoice was paid.</summary>
        Cancelled = 5
    }

    public enum BillStatus
    {
        Pending = 0,
        Approved = 1,
        Paid = 2
    }

    public enum JournalStatus
    {
        Draft = 0,
        Posted = 1,
        /// <summary>Voided: ignored by every report (the demo data uses it).</summary>
        Voided = 2,
        /// <summary>Reversed by a mirror entry: still counted, the reversal entry offsets it.</summary>
        Reversed = 3
    }

    public enum FiscalPeriodStatus
    {
        Open = 0,
        Closed = 1
    }

    public enum BankStatementStatus
    {
        Open = 0,
        Reconciled = 1
    }

    public enum InventoryMovementType
    {
        Receipt = 1,
        Shipment = 2,
        Adjustment = 3,
        Transfer = 4
    }
}
