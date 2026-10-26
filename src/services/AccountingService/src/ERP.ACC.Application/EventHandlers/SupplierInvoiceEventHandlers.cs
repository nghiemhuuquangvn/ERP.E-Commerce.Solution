using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Application.EventHandlers
{
    /// <summary>
    /// PurchaseOrderService's <c>SupplierInvoiceApprovedDomainEvent</c> (ROADMAP P1-8): a supplier invoice matched against the order and the
    /// receipts (or approved despite a variance) and is ready to be paid. This replaces the bill that used to be created for the whole order
    /// when it was approved, before any goods or invoice existed.
    /// </summary>
    public class SupplierInvoiceApprovedIntegrationEvent : INotification
    {
        public Guid SupplierInvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public Guid SupplierId { get; set; }
        public Guid PurchaseOrderId { get; set; }
        public string PONumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal NetAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal ReceivedValue { get; set; }
    }

    /// <summary>The supplier credited a return (<c>VendorReturnCreditedDomainEvent</c>).</summary>
    public class VendorReturnCreditedIntegrationEvent : INotification
    {
        public Guid VendorReturnId { get; set; }
        public string ReturnNumber { get; set; } = string.Empty;
        public Guid PurchaseOrderId { get; set; }
        public Guid SupplierId { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public string CreditNoteNumber { get; set; } = string.Empty;
        public decimal CreditAmount { get; set; }
    }

    /// <summary>
    /// Books the bill of an approved supplier invoice: gross = net + tax as invoiced, VAT split as invoiced, approved at once (a person already
    /// approved the invoice in Purchasing), posted Dr GRNI (received value) / Dr price variance / Dr VAT input / Cr Accounts payable.
    /// Idempotent per supplier invoice. An invoice in a currency other than the books' is left as a Pending bill for a person to convert.
    /// </summary>
    public class SupplierInvoiceApprovedIntegrationEventHandler : INotificationHandler<SupplierInvoiceApprovedIntegrationEvent>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly IVatPolicy? _vat;
        private readonly ILogger<SupplierInvoiceApprovedIntegrationEventHandler> _logger;

        public SupplierInvoiceApprovedIntegrationEventHandler(
            IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<SupplierInvoiceApprovedIntegrationEventHandler> logger, IVatPolicy? vat = null)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
            _vat = vat;
        }

        public async Task Handle(SupplierInvoiceApprovedIntegrationEvent e, CancellationToken ct)
        {
            if (await _uow.Bills.GetBySupplierInvoiceIdAsync(e.SupplierInvoiceId, ct) != null)
            {
                _logger.LogInformation("Bill for supplier invoice {Invoice} already exists; event ignored", e.InvoiceNumber);
                return;
            }

            var net = Math.Round(e.NetAmount, 2);
            var tax = Math.Round(e.TaxAmount, 2);
            var rate = net == 0m ? 0m : Math.Round(tax / net * 100m, 2);

            // the bill number is unique per tenant: the order number plus the supplier's invoice number (an invoice number is unique per supplier, an order has one supplier)
            var billNumber = string.IsNullOrWhiteSpace(e.PONumber) ? e.InvoiceNumber : $"{e.PONumber}/{e.InvoiceNumber}";
            var bill = Bill.Create(Truncate(billNumber, 50), e.SupplierId, e.PurchaseOrderId, net + tax, e.DueDate);
            bill.ApplyVat(net, rate, tax);
            bill.SupplierInvoiceId = e.SupplierInvoiceId;
            bill.ReceivedValue = e.ReceivedValue;
            bill.CreatedBy = "supplier invoice";

            var baseCurrency = _vat == null ? "VND" : (await _vat.GetAsync(ct)).BaseCurrency;
            var inBaseCurrency = string.IsNullOrWhiteSpace(e.CurrencyCode) || string.Equals(e.CurrencyCode, baseCurrency, StringComparison.OrdinalIgnoreCase);

            await _uow.Bills.AddAsync(bill, ct);
            if (inBaseCurrency)
            {
                bill.Approve();
                await _posting.TryPostAsync(_logger, PostingRules.ForBillApproval(bill, DateTime.UtcNow), ct);
            }
            else
            {
                _logger.LogWarning("Supplier invoice {Invoice} is in {Currency}, the books are in {Base}: the bill is left Pending for a person to convert",
                    e.InvoiceNumber, e.CurrencyCode, baseCurrency);
            }

            await _uow.SaveChangesAsync(ct);
        }

        private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    }

    /// <summary>
    /// A debit note for goods sent back: Dr Accounts payable / Cr GRNI (the return already reversed the receipt accrual in stock), and the credit
    /// comes off the open balance of the order's approved bills, oldest due date first. Idempotent per return.
    /// </summary>
    public class VendorReturnCreditedIntegrationEventHandler : INotificationHandler<VendorReturnCreditedIntegrationEvent>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<VendorReturnCreditedIntegrationEventHandler> _logger;

        public VendorReturnCreditedIntegrationEventHandler(IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<VendorReturnCreditedIntegrationEventHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task Handle(VendorReturnCreditedIntegrationEvent e, CancellationToken ct)
        {
            var request = PostingRules.ForDebitNote(e.VendorReturnId, e.ReturnNumber, e.CreditNoteNumber, e.CreditAmount, DateTime.UtcNow);
            if (request == null)
            {
                _logger.LogWarning("Vendor return {Return} was credited for no amount; nothing booked", e.ReturnNumber);
                return;
            }

            // posting is idempotent per source (the entry exists = the credit was already applied): apply the credit to the bills only the first time
            var existing = await _uow.JournalEntries.GetBySourceKeyAsync(JournalEntry.BuildSourceKey(PostingRules.DebitNote, e.VendorReturnId), ct);
            if (existing != null)
            {
                _logger.LogInformation("Debit note for return {Return} already booked; event ignored", e.ReturnNumber);
                return;
            }

            var remaining = Math.Round(e.CreditAmount, 2);
            foreach (var bill in await _uow.Bills.GetOpenApprovedByPurchaseOrderIdAsync(e.PurchaseOrderId, ct))
            {
                if (remaining <= 0m) break;
                remaining -= bill.ApplyCredit(remaining);
                await _uow.Bills.UpdateAsync(bill, ct);
            }

            await _posting.TryPostAsync(_logger, request, ct);
            await _uow.SaveChangesAsync(ct);
        }
    }
}
