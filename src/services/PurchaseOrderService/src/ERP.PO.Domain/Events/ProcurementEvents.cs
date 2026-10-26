using ERP.Shared.Core.Domain;

namespace ERP.PO.Domain.Events;

/// <summary>
/// A supplier invoice was matched (or approved despite a variance) and is ready to be paid (ROADMAP P1-8). Accounting books the bill
/// from it: net and tax as invoiced, and <see cref="ReceivedValue"/> (invoiced quantity at the order price) so the receipt accrual is
/// cleared at the price it was booked and any price difference goes to the price-variance account.
/// </summary>
public class SupplierInvoiceApprovedDomainEvent : IDomainEvent, ITenantEvent
{
    public Guid TenantId { get; set; }

    public Guid SupplierInvoiceId { get; }
    public string InvoiceNumber { get; }
    public Guid SupplierId { get; }
    public Guid PurchaseOrderId { get; }
    public string PONumber { get; }
    public DateTime InvoiceDate { get; }
    public DateTime DueDate { get; }
    public string CurrencyCode { get; }
    public decimal NetAmount { get; }
    public decimal TaxAmount { get; }
    public decimal ReceivedValue { get; }
    public DateTime OccurredOn { get; }

    public SupplierInvoiceApprovedDomainEvent(
        Guid supplierInvoiceId, string invoiceNumber, Guid supplierId, Guid purchaseOrderId, string poNumber, DateTime invoiceDate,
        DateTime dueDate, string currencyCode, decimal netAmount, decimal taxAmount, decimal receivedValue)
    {
        SupplierInvoiceId = supplierInvoiceId;
        InvoiceNumber = invoiceNumber;
        SupplierId = supplierId;
        PurchaseOrderId = purchaseOrderId;
        PONumber = poNumber;
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        CurrencyCode = currencyCode;
        NetAmount = netAmount;
        TaxAmount = taxAmount;
        ReceivedValue = receivedValue;
        OccurredOn = DateTime.UtcNow;
    }
}

/// <summary>Goods leave for the supplier. Warehouse issues the stock; Accounting reverses the receipt accrual (via Warehouse's movement).</summary>
public class VendorReturnShippedDomainEvent : IDomainEvent, ITenantEvent
{
    public Guid TenantId { get; set; }

    public Guid VendorReturnId { get; }
    public string ReturnNumber { get; }
    public Guid PurchaseOrderId { get; }
    public string PONumber { get; }
    public Guid SupplierId { get; }
    public string CurrencyCode { get; }
    public List<ReturnedLine> Lines { get; }
    public DateTime OccurredOn { get; }

    public VendorReturnShippedDomainEvent(Guid vendorReturnId, string returnNumber, Guid purchaseOrderId, string poNumber, Guid supplierId, string currencyCode, List<ReturnedLine> lines)
    {
        VendorReturnId = vendorReturnId;
        ReturnNumber = returnNumber;
        PurchaseOrderId = purchaseOrderId;
        PONumber = poNumber;
        SupplierId = supplierId;
        CurrencyCode = currencyCode;
        Lines = lines;
        OccurredOn = DateTime.UtcNow;
    }

    public class ReturnedLine
    {
        public string ProductCode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}

/// <summary>The supplier credited the return: Accounting books the debit note (accounts payable down, receipt accrual cleared).</summary>
public class VendorReturnCreditedDomainEvent : IDomainEvent, ITenantEvent
{
    public Guid TenantId { get; set; }

    public Guid VendorReturnId { get; }
    public string ReturnNumber { get; }
    public Guid PurchaseOrderId { get; }
    public string PONumber { get; }
    public Guid SupplierId { get; }
    public string CurrencyCode { get; }
    public string CreditNoteNumber { get; }
    public decimal CreditAmount { get; }
    public DateTime OccurredOn { get; }

    public VendorReturnCreditedDomainEvent(Guid vendorReturnId, string returnNumber, Guid purchaseOrderId, string poNumber, Guid supplierId, string currencyCode, string creditNoteNumber, decimal creditAmount)
    {
        VendorReturnId = vendorReturnId;
        ReturnNumber = returnNumber;
        PurchaseOrderId = purchaseOrderId;
        PONumber = poNumber;
        SupplierId = supplierId;
        CurrencyCode = currencyCode;
        CreditNoteNumber = creditNoteNumber;
        CreditAmount = creditAmount;
        OccurredOn = DateTime.UtcNow;
    }
}
