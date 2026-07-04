using ERP.ACC.Application.Commands;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using MediatR;

namespace ERP.ACC.Application.Handlers
{
    public class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, AccountDto>
    {
        private readonly IAccountRepository _accounts;

        public CreateAccountCommandHandler(IAccountRepository accounts) => _accounts = accounts;

        public async Task<AccountDto> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
        {
            var account = Account.Create(request.AccountNumber, request.Name, request.AccountType, request.NormalBalance);
            await _accounts.AddAsync(account, cancellationToken);
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
        private readonly IInvoiceRepository _invoices;

        public CreateInvoiceCommandHandler(IInvoiceRepository invoices) => _invoices = invoices;

        public async Task<InvoiceDto> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
        {
            var invoice = Invoice.Create(
                request.InvoiceNumber,
                request.CustomerId,
                request.OrderId,
                request.Amount,
                request.DueDate);

            await _invoices.AddAsync(invoice, cancellationToken);
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
        private readonly IInvoiceRepository _invoices;

        public MarkInvoicePaidCommandHandler(IInvoiceRepository invoices) => _invoices = invoices;

        public async Task<InvoiceDto> Handle(MarkInvoicePaidCommand request, CancellationToken cancellationToken)
        {
            var invoice = await _invoices.GetByIdAsync(request.InvoiceId, cancellationToken)
                ?? throw new InvalidOperationException($"Invoice {request.InvoiceId} not found.");

            invoice.MarkPaid();
            await _invoices.UpdateAsync(invoice, cancellationToken);
            return CreateInvoiceCommandHandler.MapInvoiceToDto(invoice);
        }
    }

    public class CreateBillCommandHandler : IRequestHandler<CreateBillCommand, BillDto>
    {
        private readonly IBillRepository _bills;

        public CreateBillCommandHandler(IBillRepository bills) => _bills = bills;

        public async Task<BillDto> Handle(CreateBillCommand request, CancellationToken cancellationToken)
        {
            var bill = Bill.Create(
                request.BillNumber,
                request.SupplierId,
                request.POId,
                request.Amount,
                request.DueDate);

            await _bills.AddAsync(bill, cancellationToken);
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
        private readonly IBillRepository _bills;

        public ApproveBillCommandHandler(IBillRepository bills) => _bills = bills;

        public async Task<BillDto> Handle(ApproveBillCommand request, CancellationToken cancellationToken)
        {
            var bill = await _bills.GetByIdAsync(request.BillId, cancellationToken)
                ?? throw new InvalidOperationException($"Bill {request.BillId} not found.");

            bill.Approve();
            await _bills.UpdateAsync(bill, cancellationToken);
            return CreateBillCommandHandler.MapBillToDto(bill);
        }
    }

    public class MarkBillPaidCommandHandler : IRequestHandler<MarkBillPaidCommand, BillDto>
    {
        private readonly IBillRepository _bills;

        public MarkBillPaidCommandHandler(IBillRepository bills) => _bills = bills;

        public async Task<BillDto> Handle(MarkBillPaidCommand request, CancellationToken cancellationToken)
        {
            var bill = await _bills.GetByIdAsync(request.BillId, cancellationToken)
                ?? throw new InvalidOperationException($"Bill {request.BillId} not found.");

            bill.MarkPaid();
            await _bills.UpdateAsync(bill, cancellationToken);
            return CreateBillCommandHandler.MapBillToDto(bill);
        }
    }

    public class PostJournalEntryCommandHandler : IRequestHandler<PostJournalEntryCommand, JournalEntryDto>
    {
        private readonly IJournalEntryRepository _journalEntries;
        private readonly IFiscalPeriodRepository _fiscalPeriods;

        public PostJournalEntryCommandHandler(
            IJournalEntryRepository journalEntries,
            IFiscalPeriodRepository fiscalPeriods)
        {
            _journalEntries = journalEntries;
            _fiscalPeriods = fiscalPeriods;
        }

        public async Task<JournalEntryDto> Handle(PostJournalEntryCommand request, CancellationToken cancellationToken)
        {
            var fiscalPeriod = await _fiscalPeriods.GetByIdAsync(request.FiscalPeriodId, cancellationToken)
                ?? throw new InvalidOperationException($"Fiscal period {request.FiscalPeriodId} not found.");

            if (fiscalPeriod.Status != (int)FiscalPeriodStatus.Open)
                throw new InvalidOperationException($"Fiscal period {request.FiscalPeriodId} is closed. Cannot post journal entries.");

            var totalDebit = request.Lines.Sum(l => l.Debit);
            var totalCredit = request.Lines.Sum(l => l.Credit);

            if (totalDebit != totalCredit)
                throw new InvalidOperationException($"Journal entry is not balanced. Total debits ({totalDebit}) must equal total credits ({totalCredit}).");

            var entry = JournalEntry.Create(request.EntryNumber, request.FiscalPeriodId, request.Description);
            entry.Post(totalDebit, totalCredit);

            foreach (var line in request.Lines)
                entry.AddLine(line.AccountId, line.Debit, line.Credit, line.Description);

            await _journalEntries.AddAsync(entry, cancellationToken);

            return new JournalEntryDto
            {
                Id = entry.Id,
                EntryNumber = entry.EntryNumber,
                FiscalPeriodId = entry.FiscalPeriodId,
                Description = entry.Description,
                TotalDebit = entry.TotalDebit,
                TotalCredit = entry.TotalCredit,
                CreatedAt = entry.CreatedAt,
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
        private readonly IFiscalPeriodRepository _fiscalPeriods;

        public OpenFiscalPeriodCommandHandler(IFiscalPeriodRepository fiscalPeriods) => _fiscalPeriods = fiscalPeriods;

        public async Task<FiscalPeriodDto> Handle(OpenFiscalPeriodCommand request, CancellationToken cancellationToken)
        {
            var period = FiscalPeriod.Create(request.PeriodName, request.StartDate, request.EndDate);
            await _fiscalPeriods.AddAsync(period, cancellationToken);
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
        private readonly IFiscalPeriodRepository _fiscalPeriods;

        public CloseFiscalPeriodCommandHandler(IFiscalPeriodRepository fiscalPeriods) => _fiscalPeriods = fiscalPeriods;

        public async Task<FiscalPeriodDto> Handle(CloseFiscalPeriodCommand request, CancellationToken cancellationToken)
        {
            var period = await _fiscalPeriods.GetByIdAsync(request.FiscalPeriodId, cancellationToken)
                ?? throw new InvalidOperationException($"Fiscal period {request.FiscalPeriodId} not found.");

            period.Close();
            await _fiscalPeriods.UpdateAsync(period, cancellationToken);
            return OpenFiscalPeriodCommandHandler.MapFiscalPeriodToDto(period);
        }
    }
}
