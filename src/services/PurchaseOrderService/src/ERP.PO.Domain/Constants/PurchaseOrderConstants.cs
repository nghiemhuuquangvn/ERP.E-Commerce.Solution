namespace ERP.PO.Domain.Constants;

public static class PurchaseOrderConstants
{
    public const int PONumberMaxLength = 20;
    public const int ProductCodeMaxLength = 50;
    public const int ProductNameMaxLength = 200;
    public const int NotesMaxLength = 1000;
    public const int SupplierCodeMaxLength = 20;
    public const int SupplierNameMaxLength = 200;

    public const string DefaultCurrency = "USD";

    public static class Status
    {
        public const string Draft = "DRAFT";
        public const string Submitted = "SUBMITTED";
        public const string Approved = "APPROVED";
        public const string Ordered = "ORDERED";
        public const string PartiallyReceived = "PARTIALLY_RECEIVED";
        public const string Received = "RECEIVED";
        public const string Cancelled = "CANCELLED";
    }

    public static class Policy
    {
        public const string CanCreatePO = "CanCreatePO";
        public const string CanApprovePO = "CanApprovePO";
        public const string CanCancelPO = "CanCancelPO";
        public const string CanReceivePO = "CanReceivePO";
    }
}
