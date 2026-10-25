using ERP.ACC.Application.Commands;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Application.Handlers
{
    public class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, AccountDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public CreateAccountCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<AccountDto> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
        {
            var number = request.AccountNumber.Trim();
            if ((await _uow.Accounts.GetByNumbersAsync(new[] { number }, cancellationToken)).Count > 0)
                throw new BusinessRuleException("Accounting", $"Account number {number} already exists.");

            Account? parent = null;
            if (request.ParentAccountId is { } parentId)
            {
                parent = await _uow.Accounts.GetByIdAsync(parentId, cancellationToken)
                    ?? throw new BusinessRuleException("Accounting", $"Parent account {parentId} does not exist.");
                if (!parent.IsActive)
                    throw new BusinessRuleException("Accounting", $"Parent account {parent.AccountNumber} is inactive.");
            }

            Account account;
            try
            {
                account = Account.Create(number, request.Name.Trim(), request.AccountType, request.NormalBalance, parent);
                account.SetCashFlow(request.IsCash, request.CashFlowCategory);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
            {
                throw new BusinessRuleException("Accounting", ex.Message);
            }

            await _uow.Accounts.AddAsync(account, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return MapAccountToDto(account);
        }

        internal static AccountDto MapAccountToDto(Account a, bool isParent = false) => new()
        {
            Id = a.Id,
            AccountNumber = a.AccountNumber,
            Name = a.Name,
            AccountType = ((AccountType)a.AccountType).ToString(),
            NormalBalance = ((NormalBalance)a.NormalBalance).ToString(),
            CreatedAt = a.CreatedAt,
            IsActive = a.IsActive,
            ParentAccountId = a.ParentAccountId,
            Level = a.Level,
            IsControl = a.IsControl,
            AccountClass = a.AccountClass,
            IsPostable = a.IsActive && !isParent,
            IsCash = a.IsCash,
            CashFlowCategory = a.EffectiveCashFlowCategory.ToString(),
        };
    }

    public class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, InvoiceDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<CreateInvoiceCommandHandler> _logger;
        private readonly IVatPolicy? _vat;
        private readonly IVatLineRepository? _vatLines;

        public CreateInvoiceCommandHandler(IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<CreateInvoiceCommandHandler> logger, IVatPolicy? vat = null, IVatLineRepository? vatLines = null)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
            _vat = vat;
            _vatLines = vatLines;
        }

        public async Task<InvoiceDto> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
        {
            var policy = _vat == null ? new VatSettings(0m, true) : await _vat.GetAsync(cancellationToken);
            VatSplit split;
            VatLinesSplit? lines = null;
            if (request.Lines is { Count: > 0 })
            {
                // the document is the sum of its lines (ROADMAP P1-3b)
                lines = VatLineSupport.SplitOrRefuse(request.Lines, policy.PricesIncludeTax);
                split = new VatSplit(lines.Net, lines.SummaryRate, lines.Tax, lines.Gross);
            }
            else
            {
                try
                {
                    split = VatCalculator.Split(request.Amount, request.TaxRatePercent ?? policy.RatePercent, policy.PricesIncludeTax);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    throw new BusinessRuleException("Accounting", ex.Message);
                }
            }

            // Amount is the gross the customer owes; with prices excluding tax the VAT is added to the order total
            var invoice = Invoice.Create(
                request.InvoiceNumber,
                request.CustomerId,
                request.OrderId,
                split.Gross,
                request.DueDate);
            invoice.ApplyVat(split.Net, split.RatePercent, split.Tax);

            await _uow.Invoices.AddAsync(invoice, cancellationToken);
            if (lines != null && _vatLines != null)
                await _vatLines.ReplaceAsync(VatLine.InvoiceDocument, invoice.Id, VatLineSupport.ToEntities(VatLine.InvoiceDocument, invoice.Id, lines), cancellationToken);
            // Revenue is recognised when the invoice is created; the entry commits with the invoice.
            var entry = await _posting.TryPostAsync(_logger, PostingRules.ForInvoice(invoice), cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            var dto = MapInvoiceToDto(invoice);
            if (entry == null)
            {
                dto.PostedToLedger = false;
                dto.PostingWarning = PostingWarnings.NotPosted;
            }
            return dto;
        }

        internal static InvoiceDto MapInvoiceToDto(Invoice i) => new()
        {
            Id = i.Id,
            InvoiceNumber = i.InvoiceNumber,
            CustomerId = i.CustomerId,
            OrderId = i.OrderId,
            Amount = i.Amount,
            NetAmount = i.EffectiveNet,
            TaxRatePercent = i.TaxRatePercent,
            TaxAmount = i.TaxAmount,
            DueDate = i.DueDate,
            Status = ((InvoiceStatus)i.Status).ToString(),
            CreatedAt = i.CreatedAt
        };
    }

    public class AdjustInvoiceVatCommandHandler : IRequestHandler<AdjustInvoiceVatCommand, InvoiceDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<AdjustInvoiceVatCommandHandler> _logger;
        private readonly IEInvoiceRepository? _einvoices;
        private readonly IVatLineRepository? _vatLines;

        public AdjustInvoiceVatCommandHandler(IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<AdjustInvoiceVatCommandHandler> logger, IEInvoiceRepository? einvoices = null, IVatLineRepository? vatLines = null)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
            _einvoices = einvoices;
            _vatLines = vatLines;
        }

        public async Task<InvoiceDto> Handle(AdjustInvoiceVatCommand request, CancellationToken cancellationToken)
        {
            var invoice = await _uow.Invoices.GetByIdAsync(request.InvoiceId, cancellationToken)
                ?? throw new KeyNotFoundException($"Invoice {request.InvoiceId} not found.");
            if (invoice.Status == (int)InvoiceStatus.Cancelled)
                throw new BusinessRuleException("Accounting", $"Invoice {invoice.InvoiceNumber} is cancelled: its VAT cannot be adjusted.");
            var withLines = request.Lines is { Count: > 0 };
            if (!withLines && (request.TaxRatePercent < 0m || request.TaxRatePercent > 100m))
                throw new BusinessRuleException("Accounting", "The VAT rate must be between 0 and 100.");
            // an issued e-invoice declares the VAT to the tax authority: cancel it before the split can change
            if (!request.ForReplacement && _einvoices != null && await _einvoices.GetActiveByInvoiceAsync(invoice.Id, cancellationToken) is { Status: (int)EInvoiceStatus.Issued })
                throw new BusinessRuleException("Accounting", $"Invoice {invoice.InvoiceNumber} has an issued e-invoice: cancel it before changing the VAT.");

            // the gross is what the customer owes and may already be paid: only the split changes
            VatLinesSplit? lines = withLines ? VatLineSupport.SplitOfExistingGross(request.Lines, invoice.Amount) : null;
            var split = lines != null ? new VatSplit(lines.Net, lines.SummaryRate, lines.Tax, lines.Gross) : VatCalculator.Split(invoice.Amount, request.TaxRatePercent, pricesIncludeTax: true);
            var oldTax = invoice.TaxAmount;
            invoice.ApplyVat(split.Net, split.RatePercent, split.Tax);

            var delta = split.Tax - oldTax;
            if (delta != 0m)
            {
                var entry = await _posting.TryPostAsync(_logger, PostingRules.ForInvoiceVatAdjustment(invoice, delta, DateTime.UtcNow), cancellationToken);
                if (entry == null)
                    throw new BusinessRuleException("Accounting", "The VAT adjustment could not be posted: complete the posting profile (Sales revenue and VAT output).");
            }
            if (_vatLines != null)
                await _vatLines.ReplaceAsync(VatLine.InvoiceDocument, invoice.Id, lines != null ? VatLineSupport.ToEntities(VatLine.InvoiceDocument, invoice.Id, lines) : Array.Empty<VatLine>(), cancellationToken);
            await _uow.Invoices.UpdateAsync(invoice, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return CreateInvoiceCommandHandler.MapInvoiceToDto(invoice);
        }
    }

    public class MarkInvoicePaidCommandHandler : IRequestHandler<MarkInvoicePaidCommand, InvoiceDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<MarkInvoicePaidCommandHandler> _logger;

        public MarkInvoicePaidCommandHandler(IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<MarkInvoicePaidCommandHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task<InvoiceDto> Handle(MarkInvoicePaidCommand request, CancellationToken cancellationToken)
        {
            var invoice = await _uow.Invoices.GetByIdAsync(request.InvoiceId, cancellationToken)
                ?? throw new InvalidOperationException($"Invoice {request.InvoiceId} not found.");

            // Recorded by hand, without gateway data: what is still outstanding is treated as received into the bank.
            var outstanding = invoice.Amount - invoice.AmountPaid;
            invoice.MarkPaid();
            await _uow.Invoices.UpdateAsync(invoice, cancellationToken);
            if (outstanding > 0)
                await _uow.PaymentApplications.AddAsync(PaymentApplication.Create(
                    PaymentApplication.InvoiceDocument, invoice.Id, DateTime.UtcNow, outstanding, $"manual:{invoice.Id:N}"), cancellationToken);
            if (outstanding > 0)
                await _posting.TryPostAsync(_logger, PostingRules.ForManualInvoicePayment(invoice, outstanding, DateTime.UtcNow), cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return CreateInvoiceCommandHandler.MapInvoiceToDto(invoice);
        }
    }

    public class CreateBillCommandHandler : IRequestHandler<CreateBillCommand, BillDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        private readonly IVatPolicy? _vat;
        private readonly IVatLineRepository? _vatLines;

        public CreateBillCommandHandler(IAccountingUnitOfWork uow, IVatPolicy? vat = null, IVatLineRepository? vatLines = null)
        {
            _uow = uow;
            _vat = vat;
            _vatLines = vatLines;
        }

        public async Task<BillDto> Handle(CreateBillCommand request, CancellationToken cancellationToken)
        {
            var policy = _vat == null ? new VatSettings(0m, true) : await _vat.GetAsync(cancellationToken);
            VatSplit split;
            VatLinesSplit? lines = null;
            if (request.Lines is { Count: > 0 })
            {
                lines = VatLineSupport.SplitOrRefuse(request.Lines, policy.PricesIncludeTax);
                split = new VatSplit(lines.Net, lines.SummaryRate, lines.Tax, lines.Gross);
            }
            else
            {
                try
                {
                    split = VatCalculator.Split(request.Amount, request.TaxRatePercent ?? policy.RatePercent, policy.PricesIncludeTax);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    throw new BusinessRuleException("Accounting", ex.Message);
                }
            }

            // Amount is the gross owed to the supplier; with prices excluding tax the VAT is added to the purchase order total
            var bill = Bill.Create(
                request.BillNumber,
                request.SupplierId,
                request.POId,
                split.Gross,
                request.DueDate);
            bill.ApplyVat(split.Net, split.RatePercent, split.Tax);

            await _uow.Bills.AddAsync(bill, cancellationToken);
            if (lines != null && _vatLines != null)
                await _vatLines.ReplaceAsync(VatLine.BillDocument, bill.Id, VatLineSupport.ToEntities(VatLine.BillDocument, bill.Id, lines), cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return MapBillToDto(bill);
        }

        internal static BillDto MapBillToDto(Bill b) => new()
        {
            Id = b.Id,
            BillNumber = b.BillNumber,
            SupplierId = b.SupplierId,
            POId = b.POId,
            Amount = b.Amount,
            NetAmount = b.EffectiveNet,
            TaxRatePercent = b.TaxRatePercent,
            TaxAmount = b.TaxAmount,
            DueDate = b.DueDate,
            Status = ((BillStatus)b.Status).ToString(),
            CreatedAt = b.CreatedAt
        };
    }

    public class ApproveBillCommandHandler : IRequestHandler<ApproveBillCommand, BillDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<ApproveBillCommandHandler> _logger;
        private readonly IVatLineRepository? _vatLines;

        public ApproveBillCommandHandler(IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<ApproveBillCommandHandler> logger, IVatLineRepository? vatLines = null)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
            _vatLines = vatLines;
        }

        public async Task<BillDto> Handle(ApproveBillCommand request, CancellationToken cancellationToken)
        {
            var bill = await _uow.Bills.GetByIdAsync(request.BillId, cancellationToken)
                ?? throw new InvalidOperationException($"Bill {request.BillId} not found.");

            if (request.Lines is { Count: > 0 })
            {
                var lines = VatLineSupport.SplitOfExistingGross(request.Lines, bill.Amount);
                bill.ApplyVat(lines.Net, lines.SummaryRate, lines.Tax);
                if (_vatLines != null)
                    await _vatLines.ReplaceAsync(VatLine.BillDocument, bill.Id, VatLineSupport.ToEntities(VatLine.BillDocument, bill.Id, lines), cancellationToken);
            }
            else if (request.TaxRatePercent.HasValue)
            {
                if (request.TaxRatePercent < 0m || request.TaxRatePercent > 100m)
                    throw new BusinessRuleException("Accounting", "The VAT rate must be between 0 and 100.");
                var split = VatCalculator.Split(bill.Amount, request.TaxRatePercent.Value, pricesIncludeTax: true);
                bill.ApplyVat(split.Net, split.RatePercent, split.Tax);
                if (_vatLines != null)
                    await _vatLines.ReplaceAsync(VatLine.BillDocument, bill.Id, Array.Empty<VatLine>(), cancellationToken);
            }
            bill.Approve();
            await _uow.Bills.UpdateAsync(bill, cancellationToken);
            var entry = await _posting.TryPostAsync(_logger, PostingRules.ForBillApproval(bill, DateTime.UtcNow), cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            var dto = CreateBillCommandHandler.MapBillToDto(bill);
            if (entry == null)
            {
                dto.PostedToLedger = false;
                dto.PostingWarning = PostingWarnings.NotPosted;
            }
            return dto;
        }
    }

    public class MarkBillPaidCommandHandler : IRequestHandler<MarkBillPaidCommand, BillDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<MarkBillPaidCommandHandler> _logger;

        public MarkBillPaidCommandHandler(IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<MarkBillPaidCommandHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task<BillDto> Handle(MarkBillPaidCommand request, CancellationToken cancellationToken)
        {
            var bill = await _uow.Bills.GetByIdAsync(request.BillId, cancellationToken)
                ?? throw new InvalidOperationException($"Bill {request.BillId} not found.");

            bill.MarkPaid();
            await _uow.Bills.UpdateAsync(bill, cancellationToken);
            await _uow.PaymentApplications.AddAsync(PaymentApplication.Create(
                PaymentApplication.BillDocument, bill.Id, DateTime.UtcNow, bill.Amount, $"manual:{bill.Id:N}"), cancellationToken);
            await _posting.TryPostAsync(_logger, PostingRules.ForBillPayment(bill, DateTime.UtcNow), cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return CreateBillCommandHandler.MapBillToDto(bill);
        }
    }

    public class PostJournalEntryCommandHandler : IRequestHandler<PostJournalEntryCommand, JournalEntryDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public PostJournalEntryCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<JournalEntryDto> Handle(PostJournalEntryCommand request, CancellationToken cancellationToken)
        {
            var fiscalPeriod = await _uow.FiscalPeriods.GetByIdAsync(request.FiscalPeriodId, cancellationToken)
                ?? throw new InvalidOperationException($"Fiscal period {request.FiscalPeriodId} not found.");

            if (fiscalPeriod.Status != (int)FiscalPeriodStatus.Open)
                throw new InvalidOperationException($"Fiscal period {request.FiscalPeriodId} is closed. Cannot post journal entries.");

            var totalDebit = request.Lines.Sum(l => l.Debit);
            var totalCredit = request.Lines.Sum(l => l.Credit);

            if (totalDebit != totalCredit)
                throw new InvalidOperationException($"Journal entry is not balanced. Total debits ({totalDebit}) must equal total credits ({totalCredit}).");

            // The database CHECK would reject these with a generic error; say what is wrong instead.
            if (request.Lines.Count < 2)
                throw new BusinessRuleException("Accounting", "A journal entry needs at least two lines.");
            if (request.Lines.Any(l => l.Debit < 0 || l.Credit < 0 || (l.Debit > 0) == (l.Credit > 0)))
                throw new BusinessRuleException("Accounting", "Each journal line needs exactly one positive amount, either a debit or a credit.");

            var accounts = await _uow.Accounts.GetByIdsAsync(request.Lines.Select(l => l.AccountId).Distinct(), cancellationToken);
            var parentIds = await AccountRules.ParentIdsAsync(_uow.Accounts, cancellationToken);
            foreach (var accountId in request.Lines.Select(l => l.AccountId).Distinct())
            {
                var account = accounts.FirstOrDefault(a => a.Id == accountId)
                    ?? throw new BusinessRuleException("Accounting", $"Account {accountId} does not exist.");
                AccountRules.EnsurePostable(account, parentIds);
            }

            var entryDate = (request.EntryDate ?? DateTime.UtcNow).Date;
            if (entryDate < fiscalPeriod.StartDate.Date || entryDate > fiscalPeriod.EndDate.Date)
                throw new BusinessRuleException("Accounting", $"Entry date {entryDate:yyyy-MM-dd} is outside fiscal period {fiscalPeriod.PeriodName}.");

            var entry = JournalEntry.Create(request.EntryNumber, request.FiscalPeriodId, request.Description);
            entry.EntryDate = entryDate;
            entry.Post(totalDebit, totalCredit);

            foreach (var line in request.Lines)
                entry.AddLine(line.AccountId, line.Debit, line.Credit, line.Description);

            await _uow.JournalEntries.AddAsync(entry, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            return new JournalEntryDto
            {
                Id = entry.Id,
                EntryNumber = entry.EntryNumber,
                FiscalPeriodId = entry.FiscalPeriodId,
                Description = entry.Description,
                TotalDebit = entry.TotalDebit,
                TotalCredit = entry.TotalCredit,
                CreatedAt = entry.CreatedAt,
                EntryDate = entry.EntryDate,
                Status = ((JournalStatus)entry.Status).ToString(),
                Lines = entry.Lines.Select(l => new JournalLineDto
                {
                    AccountId = l.AccountId,
                    Debit = l.Debit,
                    Credit = l.Credit,
                    Description = l.Description
                }).ToList()
            };
        }
    }

    public class OpenFiscalPeriodCommandHandler : IRequestHandler<OpenFiscalPeriodCommand, FiscalPeriodDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public OpenFiscalPeriodCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<FiscalPeriodDto> Handle(OpenFiscalPeriodCommand request, CancellationToken cancellationToken)
        {
            var period = FiscalPeriod.Create(request.PeriodName, request.StartDate, request.EndDate);
            await _uow.FiscalPeriods.AddAsync(period, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return MapFiscalPeriodToDto(period);
        }

        internal static FiscalPeriodDto MapFiscalPeriodToDto(FiscalPeriod p) => new()
        {
            Id = p.Id,
            PeriodName = p.PeriodName,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            Status = ((FiscalPeriodStatus)p.Status).ToString(),
            CreatedAt = p.CreatedAt
        };
    }

}
