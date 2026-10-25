using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Application.Posting
{
    public static class PostingWarnings
    {
        public const string NotPosted =
            "The document was saved but its journal entry was not posted: there is no open fiscal period for its date, or the posting profile is incomplete. Open the period or complete the profile, then post the missing entries.";
    }

    public class PostingGapDto
    {
        /// <summary>"Invoice" or "Bill".</summary>
        public string DocumentType { get; set; } = string.Empty;
        public Guid DocumentId { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }

        /// <summary>Set by the repost: whether the entry was posted now, and its number.</summary>
        public bool? Posted { get; set; }
        public string? EntryNumber { get; set; }
    }

    /// <summary>Invoices (not cancelled) and approved bills that have no journal entry.</summary>
    public class GetPostingGapsQuery : IRequest<List<PostingGapDto>> { }

    /// <summary>Posts the entries of the documents found by <see cref="GetPostingGapsQuery"/>; safe to repeat (an entry is posted once per document).</summary>
    public class RepostPostingGapsCommand : IRequest<List<PostingGapDto>> { }

    internal static class PostingGapFinder
    {
        public static async Task<(List<(Invoice Invoice, PostingGapDto Gap)> Invoices, List<(Bill Bill, PostingGapDto Gap)> Bills)> FindAsync(IAccountingUnitOfWork uow, CancellationToken ct)
        {
            var invoices = (await uow.Invoices.GetAllAsync(ct)).Where(i => i.Status != (int)InvoiceStatus.Cancelled && i.Amount > 0m).ToList();
            var bills = (await uow.Bills.GetAllAsync(ct)).Where(b => b.Status >= (int)BillStatus.Approved && b.Amount > 0m).ToList();
            var keys = invoices.Select(i => JournalEntry.BuildSourceKey(PostingRules.Invoice, i.Id)).Concat(bills.Select(b => JournalEntry.BuildSourceKey(PostingRules.Bill, b.Id))).ToList();
            var existing = await uow.JournalEntries.ExistingSourceKeysAsync(keys, ct);
            return (
                invoices.Where(i => !existing.Contains(JournalEntry.BuildSourceKey(PostingRules.Invoice, i.Id)))
                    .Select(i => (i, new PostingGapDto { DocumentType = "Invoice", DocumentId = i.Id, Number = i.InvoiceNumber, Date = i.CreatedAt.Date, Amount = i.Amount })).ToList(),
                bills.Where(b => !existing.Contains(JournalEntry.BuildSourceKey(PostingRules.Bill, b.Id)))
                    .Select(b => (b, new PostingGapDto { DocumentType = "Bill", DocumentId = b.Id, Number = b.BillNumber, Date = (b.ApprovedOn ?? b.CreatedAt).Date, Amount = b.Amount })).ToList());
        }
    }

    public class GetPostingGapsQueryHandler : IRequestHandler<GetPostingGapsQuery, List<PostingGapDto>>
    {
        private readonly IAccountingUnitOfWork _uow;

        public GetPostingGapsQueryHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<List<PostingGapDto>> Handle(GetPostingGapsQuery request, CancellationToken cancellationToken)
        {
            var (invoices, bills) = await PostingGapFinder.FindAsync(_uow, cancellationToken);
            return invoices.Select(x => x.Gap).Concat(bills.Select(x => x.Gap)).OrderBy(g => g.Date).ToList();
        }
    }

    public class RepostPostingGapsCommandHandler : IRequestHandler<RepostPostingGapsCommand, List<PostingGapDto>>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<RepostPostingGapsCommandHandler> _logger;

        public RepostPostingGapsCommandHandler(IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<RepostPostingGapsCommandHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task<List<PostingGapDto>> Handle(RepostPostingGapsCommand request, CancellationToken cancellationToken)
        {
            var (invoices, bills) = await PostingGapFinder.FindAsync(_uow, cancellationToken);
            var result = new List<PostingGapDto>();
            foreach (var (invoice, gap) in invoices)
            {
                var entry = await _posting.TryPostAsync(_logger, PostingRules.ForInvoice(invoice), cancellationToken);
                gap.Posted = entry != null;
                gap.EntryNumber = entry?.EntryNumber;
                result.Add(gap);
            }
            foreach (var (bill, gap) in bills)
            {
                var entry = await _posting.TryPostAsync(_logger, PostingRules.ForBillApproval(bill, bill.ApprovedOn ?? DateTime.UtcNow), cancellationToken);
                gap.Posted = entry != null;
                gap.EntryNumber = entry?.EntryNumber;
                result.Add(gap);
            }
            await _uow.SaveChangesAsync(cancellationToken);
            return result.OrderBy(g => g.Date).ToList();
        }
    }
}
