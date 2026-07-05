using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Queries;
using ERP.Shared.Infrastructure.Caching;
using MediatR;

namespace ERP.ACC.Application.Handlers
{
    public class GetAccountQueryHandler : IRequestHandler<GetAccountQuery, AccountDto?>
    {
        private readonly IAccountRepository _accounts;
        private readonly ICacheService _cache;

        public GetAccountQueryHandler(IAccountRepository accounts, ICacheService cache)
        {
            _accounts = accounts;
            _cache = cache;
        }

        public async Task<AccountDto?> Handle(GetAccountQuery request, CancellationToken cancellationToken)
        {
            var cacheKey = $"acc:account:{request.AccountId}";
            var cached = await _cache.GetAsync<AccountDto>(cacheKey, cancellationToken);
            if (cached != null) return cached;

            var account = await _accounts.GetByIdAsync(request.AccountId, cancellationToken);
            if (account == null) return null;

            var dto = CreateAccountCommandHandler.MapAccountToDto(account);
            await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);
            return dto;
        }
    }

    public class ListAccountsQueryHandler : IRequestHandler<ListAccountsQuery, List<AccountDto>>
    {
        private readonly IAccountRepository _accounts;
        private readonly ICacheService _cache;

        public ListAccountsQueryHandler(IAccountRepository accounts, ICacheService cache)
        {
            _accounts = accounts;
            _cache = cache;
        }

        public async Task<List<AccountDto>> Handle(ListAccountsQuery request, CancellationToken cancellationToken)
        {
            const string cacheKey = "acc:accounts:all";
            var cached = await _cache.GetAsync<List<AccountDto>>(cacheKey, cancellationToken);
            if (cached != null) return cached;

            var accounts = await _accounts.GetAllAsync(cancellationToken);
            var dtos = accounts.Select(CreateAccountCommandHandler.MapAccountToDto).ToList();
            await _cache.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(5), cancellationToken);
            return dtos;
        }
    }

    public class GetInvoiceQueryHandler : IRequestHandler<GetInvoiceQuery, InvoiceDto?>
    {
        private readonly IInvoiceRepository _invoices;

        public GetInvoiceQueryHandler(IInvoiceRepository invoices) => _invoices = invoices;

        public async Task<InvoiceDto?> Handle(GetInvoiceQuery request, CancellationToken cancellationToken)
        {
            var invoice = await _invoices.GetByIdAsync(request.InvoiceId, cancellationToken);
            return invoice == null ? null : CreateInvoiceCommandHandler.MapInvoiceToDto(invoice);
        }
    }

    public class ListInvoicesQueryHandler : IRequestHandler<ListInvoicesQuery, List<InvoiceDto>>
    {
        private readonly IInvoiceRepository _invoices;

        public ListInvoicesQueryHandler(IInvoiceRepository invoices) => _invoices = invoices;

        public async Task<List<InvoiceDto>> Handle(ListInvoicesQuery request, CancellationToken cancellationToken)
        {
            List<Domain.Entities.Invoice> invoices;

            if (request.CustomerId.HasValue)
                invoices = await _invoices.GetByCustomerIdAsync(request.CustomerId.Value, cancellationToken);
            else if (request.Status.HasValue)
                invoices = await _invoices.GetByStatusAsync(request.Status.Value, cancellationToken);
            else
                invoices = await _invoices.GetAllAsync(cancellationToken);

            return invoices
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(CreateInvoiceCommandHandler.MapInvoiceToDto)
                .ToList();
        }
    }

    public class GetBillQueryHandler : IRequestHandler<GetBillQuery, BillDto?>
    {
        private readonly IBillRepository _bills;

        public GetBillQueryHandler(IBillRepository bills) => _bills = bills;

        public async Task<BillDto?> Handle(GetBillQuery request, CancellationToken cancellationToken)
        {
            var bill = await _bills.GetByIdAsync(request.BillId, cancellationToken);
            return bill == null ? null : CreateBillCommandHandler.MapBillToDto(bill);
        }
    }

    public class ListBillsQueryHandler : IRequestHandler<ListBillsQuery, List<BillDto>>
    {
        private readonly IBillRepository _bills;

        public ListBillsQueryHandler(IBillRepository bills) => _bills = bills;

        public async Task<List<BillDto>> Handle(ListBillsQuery request, CancellationToken cancellationToken)
        {
            List<Domain.Entities.Bill> bills;

            if (request.SupplierId.HasValue)
                bills = await _bills.GetBySupplierIdAsync(request.SupplierId.Value, cancellationToken);
            else if (request.Status.HasValue)
                bills = await _bills.GetByStatusAsync(request.Status.Value, cancellationToken);
            else
                bills = await _bills.GetAllAsync(cancellationToken);

            return bills
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(CreateBillCommandHandler.MapBillToDto)
                .ToList();
        }
    }

    public class GetJournalEntryQueryHandler : IRequestHandler<GetJournalEntryQuery, JournalEntryDto?>
    {
        private readonly IJournalEntryRepository _journalEntries;

        public GetJournalEntryQueryHandler(IJournalEntryRepository journalEntries) => _journalEntries = journalEntries;

        public async Task<JournalEntryDto?> Handle(GetJournalEntryQuery request, CancellationToken cancellationToken)
        {
            var entry = await _journalEntries.GetByIdAsync(request.JournalEntryId, cancellationToken);
            return entry == null ? null : MapJournalEntryToDto(entry);
        }

        internal static JournalEntryDto MapJournalEntryToDto(Domain.Entities.JournalEntry e) => new()
        {
            Id = e.Id,
            EntryNumber = e.EntryNumber,
            FiscalPeriodId = e.FiscalPeriodId,
            Description = e.Description,
            TotalDebit = e.TotalDebit,
            TotalCredit = e.TotalCredit,
            CreatedAt = e.CreatedAt
        };
    }

    public class ListJournalEntriesQueryHandler : IRequestHandler<ListJournalEntriesQuery, List<JournalEntryDto>>
    {
        private readonly IJournalEntryRepository _journalEntries;

        public ListJournalEntriesQueryHandler(IJournalEntryRepository journalEntries) => _journalEntries = journalEntries;

        public async Task<List<JournalEntryDto>> Handle(ListJournalEntriesQuery request, CancellationToken cancellationToken)
        {
            List<Domain.Entities.JournalEntry> entries;

            if (request.FiscalPeriodId.HasValue)
                entries = await _journalEntries.GetByFiscalPeriodIdAsync(request.FiscalPeriodId.Value, cancellationToken);
            else
                entries = await _journalEntries.GetAllAsync(cancellationToken);

            return entries
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(GetJournalEntryQueryHandler.MapJournalEntryToDto)
                .ToList();
        }
    }

    public class GetFiscalPeriodQueryHandler : IRequestHandler<GetFiscalPeriodQuery, FiscalPeriodDto?>
    {
        private readonly IFiscalPeriodRepository _fiscalPeriods;

        public GetFiscalPeriodQueryHandler(IFiscalPeriodRepository fiscalPeriods) => _fiscalPeriods = fiscalPeriods;

        public async Task<FiscalPeriodDto?> Handle(GetFiscalPeriodQuery request, CancellationToken cancellationToken)
        {
            var period = await _fiscalPeriods.GetByIdAsync(request.FiscalPeriodId, cancellationToken);
            return period == null ? null : OpenFiscalPeriodCommandHandler.MapFiscalPeriodToDto(period);
        }
    }

    public class ListFiscalPeriodsQueryHandler : IRequestHandler<ListFiscalPeriodsQuery, List<FiscalPeriodDto>>
    {
        private readonly IFiscalPeriodRepository _fiscalPeriods;

        public ListFiscalPeriodsQueryHandler(IFiscalPeriodRepository fiscalPeriods) => _fiscalPeriods = fiscalPeriods;

        public async Task<List<FiscalPeriodDto>> Handle(ListFiscalPeriodsQuery request, CancellationToken cancellationToken)
        {
            var periods = await _fiscalPeriods.GetAllAsync(cancellationToken);
            return periods.Select(OpenFiscalPeriodCommandHandler.MapFiscalPeriodToDto).ToList();
        }
    }

    public class GetARAgingQueryHandler : IRequestHandler<GetARAgingQuery, List<ARAgingDto>>
    {
        private readonly IInvoiceRepository _invoices;

        public GetARAgingQueryHandler(IInvoiceRepository invoices) => _invoices = invoices;

        public async Task<List<ARAgingDto>> Handle(GetARAgingQuery request, CancellationToken cancellationToken)
        {
            var asOf = request.AsOfDate ?? DateTime.UtcNow;
            var unpaidInvoices = await _invoices.GetUnpaidInvoicesAsync(cancellationToken);

            var agingByCustomer = unpaidInvoices
                .GroupBy(i => i.CustomerId)
                .Select(g =>
                {
                    var aging = new ARAgingDto { CustomerId = g.Key };
                    foreach (var invoice in g)
                    {
                        var daysOverdue = (asOf - invoice.DueDate).Days;
                        if (daysOverdue <= 0)
                            aging.Current += invoice.Amount;
                        else if (daysOverdue <= 30)
                            aging.Days30 += invoice.Amount;
                        else if (daysOverdue <= 60)
                            aging.Days60 += invoice.Amount;
                        else
                            aging.Days90Plus += invoice.Amount;
                    }
                    aging.TotalOutstanding = aging.Current + aging.Days30 + aging.Days60 + aging.Days90Plus;
                    return aging;
                })
                .ToList();

            return agingByCustomer;
        }
    }

    public class GetAPAgingQueryHandler : IRequestHandler<GetAPAgingQuery, List<APAgingDto>>
    {
        private readonly IBillRepository _bills;

        public GetAPAgingQueryHandler(IBillRepository bills) => _bills = bills;

        public async Task<List<APAgingDto>> Handle(GetAPAgingQuery request, CancellationToken cancellationToken)
        {
            var asOf = request.AsOfDate ?? DateTime.UtcNow;
            var unpaidBills = await _bills.GetUnpaidBillsAsync(cancellationToken);

            var agingBySupplier = unpaidBills
                .GroupBy(b => b.SupplierId)
                .Select(g =>
                {
                    var aging = new APAgingDto { SupplierId = g.Key };
                    foreach (var bill in g)
                    {
                        var daysOverdue = (asOf - bill.DueDate).Days;
                        if (daysOverdue <= 0)
                            aging.Current += bill.Amount;
                        else if (daysOverdue <= 30)
                            aging.Days30 += bill.Amount;
                        else if (daysOverdue <= 60)
                            aging.Days60 += bill.Amount;
                        else
                            aging.Days90Plus += bill.Amount;
                    }
                    aging.TotalOutstanding = aging.Current + aging.Days30 + aging.Days60 + aging.Days90Plus;
                    return aging;
                })
                .ToList();

            return agingBySupplier;
        }
    }
}
