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
            var account = Account.Create(request.AccountNumber, request.Name, request.AccountType, request.NormalBalance);
            await _uow.Accounts.AddAsync(account, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return MapAccountToDto(account);
        }

        internal static AccountDto MapAccountToDto(Account a) => new()
        {
            Id = a.Id,
            AccountNumber = a.AccountNumber,
            Name = a.Name,
            AccountType = ((AccountType)a.AccountType).ToString(),
            NormalBalance = ((NormalBalance)a.NormalBalance).ToString(),
            CreatedAt = a.CreatedAt
        };
    }

    public class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, InvoiceDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<CreateInvoiceCommandHandler> _logger;

        public CreateInvoiceCommandHandler(IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<CreateInvoiceCommandHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task<InvoiceDto> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
        {
            var invoice = Invoice.Create(
                request.InvoiceNumber,
                request.CustomerId,
                request.OrderId,
                request.Amount,
                request.DueDate);

            await _uow.Invoices.AddAsync(invoice, cancellationToken);
            // Revenue is recognised when the invoice is created; the entry commits with the invoice.
            await _posting.TryPostAsync(_logger, PostingRules.ForInvoice(invoice), cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return MapInvoiceToDto(invoice);
        }

        internal static InvoiceDto MapInvoiceToDto(Invoice i) => new()
        {
            Id = i.Id,
            InvoiceNumber = i.InvoiceNumber,
            CustomerId = i.CustomerId,
            OrderId = i.OrderId,
            Amount = i.Amount,
            DueDate = i.DueDate,
            Status = ((InvoiceStatus)i.Status).ToString(),
            CreatedAt = i.CreatedAt
        };
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
                await _posting.TryPostAsync(_logger, PostingRules.ForManualInvoicePayment(invoice, outstanding, DateTime.UtcNow), cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return CreateInvoiceCommandHandler.MapInvoiceToDto(invoice);
        }
    }

    public class CreateBillCommandHandler : IRequestHandler<CreateBillCommand, BillDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public CreateBillCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<BillDto> Handle(CreateBillCommand request, CancellationToken cancellationToken)
        {
            var bill = Bill.Create(
                request.BillNumber,
                request.SupplierId,
                request.POId,
                request.Amount,
                request.DueDate);

            await _uow.Bills.AddAsync(bill, cancellationToken);
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

        public ApproveBillCommandHandler(IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<ApproveBillCommandHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task<BillDto> Handle(ApproveBillCommand request, CancellationToken cancellationToken)
        {
            var bill = await _uow.Bills.GetByIdAsync(request.BillId, cancellationToken)
                ?? throw new InvalidOperationException($"Bill {request.BillId} not found.");

            bill.Approve();
            await _uow.Bills.UpdateAsync(bill, cancellationToken);
            await _posting.TryPostAsync(_logger, PostingRules.ForBillApproval(bill, DateTime.UtcNow), cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return CreateBillCommandHandler.MapBillToDto(bill);
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
            foreach (var accountId in request.Lines.Select(l => l.AccountId).Distinct())
            {
                var account = accounts.FirstOrDefault(a => a.Id == accountId)
                    ?? throw new BusinessRuleException("Accounting", $"Account {accountId} does not exist.");
                if (!account.IsActive)
                    throw new BusinessRuleException("Accounting", $"Account {account.AccountNumber} is inactive and takes no postings.");
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

    public class CloseFiscalPeriodCommandHandler : IRequestHandler<CloseFiscalPeriodCommand, FiscalPeriodDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public CloseFiscalPeriodCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<FiscalPeriodDto> Handle(CloseFiscalPeriodCommand request, CancellationToken cancellationToken)
        {
            var period = await _uow.FiscalPeriods.GetByIdAsync(request.FiscalPeriodId, cancellationToken)
                ?? throw new InvalidOperationException($"Fiscal period {request.FiscalPeriodId} not found.");

            period.Close();
            await _uow.FiscalPeriods.UpdateAsync(period, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return OpenFiscalPeriodCommandHandler.MapFiscalPeriodToDto(period);
        }
    }
}
