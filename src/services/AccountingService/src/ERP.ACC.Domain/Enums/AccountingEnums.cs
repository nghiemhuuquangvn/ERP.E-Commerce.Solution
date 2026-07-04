namespace ERP.ACC.Domain.Enums
{
    public enum AccountType
    {
        Asset = 1,
        Liability = 2,
        Equity = 3,
        Revenue = 4,
        Expense = 5
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
        Overdue = 2
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
        Reversed = 2
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
