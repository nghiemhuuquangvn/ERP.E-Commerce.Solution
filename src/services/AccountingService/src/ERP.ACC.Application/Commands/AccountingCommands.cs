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
    }

    /// <summary>Renames an account and/or moves it under another parent (null = top level). The number, type and normal balance never change.</summary>
    public class UpdateAccountCommand : IRequest<AccountDto>
    {
        public Guid AccountId { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid? ParentAccountId { get; set; }
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
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
    }

    public class MarkInvoicePaidCommand : IRequest<InvoiceDto>
    {
        public Guid InvoiceId { get; set; }
    }

    public class CreateBillCommand : IRequest<BillDto>
    {
        public string BillNumber { get; set; } = string.Empty;
        public Guid SupplierId { get; set; }
        public Guid POId { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
    }

    public class ApproveBillCommand : IRequest<BillDto>
    {
        public Guid BillId { get; set; }
    }

    public class MarkBillPaidCommand : IRequest<BillDto>
    {
        public Guid BillId { get; set; }
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
