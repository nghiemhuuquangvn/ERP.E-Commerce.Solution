using ERP.ACC.Application.Posting;
using ERP.ACC.Application.DTOs;
using MediatR;

namespace ERP.ACC.Application.Commands
{
    public class CreateAccountCommand : IRequest<AccountDto>
    {
        public string AccountNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int AccountType { get; set; }
        public int NormalBalance { get; set; }

        /// <summary>Optional parent: the new account is its sub-account (same account type).</summary>
        public Guid? ParentAccountId { get; set; }

        public bool IsCash { get; set; }

        /// <summary>1 operating, 2 investing, 3 financing; null = the default.</summary>
        public int? CashFlowCategory { get; set; }
    }

    /// <summary>Renames an account and/or moves it under another parent (null = top level). The number, type and normal balance never change.</summary>
    public class UpdateAccountCommand : IRequest<AccountDto>
    {
        public Guid AccountId { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid? ParentAccountId { get; set; }

        /// <summary>null = unchanged.</summary>
        public bool? IsCash { get; set; }

        /// <summary>null = unchanged; 0 = back to the default; 1 operating, 2 investing, 3 financing.</summary>
        public int? CashFlowCategory { get; set; }
    }

    public class SetAccountActiveCommand : IRequest<AccountDto>
    {
        public Guid AccountId { get; set; }
        public bool Active { get; set; }
    }

    public class CreateInvoiceCommand : IRequest<InvoiceDto>
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public Guid OrderId { get; set; }

        /// <summary>The order total. With the tenant's "prices include tax" it is the gross; otherwise the net and VAT is added on top (ROADMAP P1-3).</summary>
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }

        /// <summary>Overrides the tenant's default VAT rate (percent) for this invoice; empty = the default.</summary>
        public decimal? TaxRatePercent { get; set; }

        /// <summary>VAT lines (rates 0 / 5 / 8 / 10 %, goods not subject to VAT); when given they replace <see cref="Amount"/> and the rate: the document is the sum of its lines (ROADMAP P1-3b).</summary>
        public List<VatLineInput> Lines { get; set; } = new();

        /// <summary>A document in a foreign currency (ROADMAP P1-4): the currency, the total in it (replaces <see cref="Amount"/>) and the rate (base currency per 1 foreign). Empty = the base currency.</summary>
        public string? CurrencyCode { get; set; }
        public decimal? ForeignAmount { get; set; }
        public decimal? ExchangeRate { get; set; }
    }

    /// <summary>Re-splits the invoice's gross at another VAT rate and posts the difference (Dr / Cr sales revenue against VAT output). The amount owed does not change.</summary>
    public class AdjustInvoiceVatCommand : IRequest<InvoiceDto>
    {
        public Guid InvoiceId { get; set; }
        public decimal TaxRatePercent { get; set; }

        /// <summary>VAT lines whose gross amounts add up to the invoice amount; when given the rate is ignored.</summary>
        public List<VatLineInput> Lines { get; set; } = new();

        /// <summary>Set only by the replacement of an e-invoice (ROADMAP P1-3b): the VAT may change although an e-invoice is issued, because that e-invoice is replaced next.</summary>
        public bool ForReplacement { get; set; }
    }

    public class MarkInvoicePaidCommand : IRequest<InvoiceDto>
    {
        public Guid InvoiceId { get; set; }

        /// <summary>Required for an invoice in a foreign currency (ROADMAP P1-4): the rate the money was received at; the difference to the booked rate is the realized FX gain / loss.</summary>
        public decimal? PaymentRate { get; set; }
    }

    public class CreateBillCommand : IRequest<BillDto>
    {
        public string BillNumber { get; set; } = string.Empty;
        public Guid SupplierId { get; set; }
        public Guid POId { get; set; }

        /// <summary>The purchase order total. With the tenant's "prices include tax" it is the gross; otherwise the net and VAT is added on top (ROADMAP P1-3).</summary>
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }

        /// <summary>Overrides the tenant's default VAT rate (percent) for this bill; empty = the default.</summary>
        public decimal? TaxRatePercent { get; set; }

        /// <summary>VAT lines; when given they replace <see cref="Amount"/> and the rate (ROADMAP P1-3b).</summary>
        public List<VatLineInput> Lines { get; set; } = new();

        /// <summary>A bill in a foreign currency (ROADMAP P1-4), as on the invoice.</summary>
        public string? CurrencyCode { get; set; }
        public decimal? ForeignAmount { get; set; }
        public decimal? ExchangeRate { get; set; }
    }

    public class ApproveBillCommand : IRequest<BillDto>
    {
        public Guid BillId { get; set; }

        /// <summary>Re-splits the bill's gross at this VAT rate before it is posted (the supplier's invoice shows the real rate); empty keeps the split made at creation.</summary>
        public decimal? TaxRatePercent { get; set; }

        /// <summary>VAT lines whose gross amounts add up to the bill amount; when given the rate is ignored.</summary>
        public List<VatLineInput> Lines { get; set; } = new();
    }

    /// <summary>The VAT lines of an invoice or a bill (document type 1 / 2); a document without stored lines shows its single implicit line.</summary>
    public class GetVatLinesQuery : IRequest<List<VatLineDto>>
    {
        public int DocumentType { get; set; }
        public Guid DocumentId { get; set; }
    }

    public class MarkBillPaidCommand : IRequest<BillDto>
    {
        public Guid BillId { get; set; }

        /// <summary>Required for a bill in a foreign currency (ROADMAP P1-4): the rate the supplier was paid at.</summary>
        public decimal? PaymentRate { get; set; }
    }

    public class ReverseJournalEntryCommand : IRequest<JournalEntryDto>
    {
        public Guid JournalEntryId { get; set; }
        public string Reason { get; set; } = string.Empty;

        /// <summary>Date of the mirror entry; defaults to today. A date in a closed period falls back to today (the closed books do not change).</summary>
        public DateTime? ReversalDate { get; set; }
    }

    /// <summary>Takes a hand-written entry of an open period out of the books (status Voided).</summary>
    public class VoidJournalEntryCommand : IRequest<JournalEntryDto>
    {
        public Guid JournalEntryId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class UpdatePostingProfileCommand : IRequest<PostingProfileDto>
    {
        public List<PostingMappingInput> Mappings { get; set; } = new();
    }

    public class PostJournalEntryCommand : IRequest<JournalEntryDto>
    {
        /// <summary>Accounting date; defaults to today. Must fall inside the fiscal period.</summary>
        public DateTime? EntryDate { get; set; }
        public string EntryNumber { get; set; } = string.Empty;
        public Guid FiscalPeriodId { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<JournalLineDto> Lines { get; set; } = new();
    }

    public class OpenFiscalPeriodCommand : IRequest<FiscalPeriodDto>
    {
        public string PeriodName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class CloseFiscalPeriodCommand : IRequest<FiscalPeriodDto>
    {
        public Guid FiscalPeriodId { get; set; }

        /// <summary>Write the period-end transfer (revenue and expenses to the income summary, then to retained earnings). Default true.</summary>
        public bool PostClosingEntries { get; set; } = true;
    }

    public class ReopenFiscalPeriodCommand : IRequest<FiscalPeriodDto>
    {
        public Guid FiscalPeriodId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class CreateTaxRateCommand : IRequest<TaxRateDto>
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateTaxRateCommand : IRequest<TaxRateDto>
    {
        public Guid TaxRateId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public string? Description { get; set; }
    }

    public class DeactivateTaxRateCommand : IRequest<TaxRateDto>
    {
        public Guid TaxRateId { get; set; }
    }

    public class CreateBankAccountCommand : IRequest<BankAccountDto>
    {
        public string AccountName { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public decimal OpeningBalance { get; set; }
        public string CurrencyCode { get; set; } = "VND";
        public Guid? GlAccountId { get; set; }
    }

    public class CreateBankStatementCommand : IRequest<BankStatementDto>
    {
        public Guid BankAccountId { get; set; }
        public DateTime StatementDate { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
        public string? Notes { get; set; }
    }

    public class ReconcileBankStatementCommand : IRequest<BankStatementDto>
    {
        public Guid BankStatementId { get; set; }
        public string? Notes { get; set; }
    }
}
