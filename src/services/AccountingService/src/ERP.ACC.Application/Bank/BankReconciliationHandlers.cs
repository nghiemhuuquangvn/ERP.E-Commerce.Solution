using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.ACC.Application.Bank
{
    public class BankStatementLineDto
    {
        public Guid Id { get; set; }
        public Guid BankStatementId { get; set; }
        public DateTime LineDate { get; set; }
        public string? Description { get; set; }
        public string? Reference { get; set; }

        /// <summary>Signed: a deposit is positive, a withdrawal negative.</summary>
        public decimal Amount { get; set; }
        public decimal? Balance { get; set; }

        /// <summary>Unmatched, Matched, or Posted (an entry was posted from the line).</summary>
        public string MatchStatus { get; set; } = "Unmatched";
        public Guid? MatchedJournalLineId { get; set; }
        public Guid? JournalEntryId { get; set; }
        public string? MatchedEntryNumber { get; set; }
    }

    /// <summary>A line of the bank's ledger account: debit − credit as <see cref="Amount"/> (a deposit is positive).</summary>
    public class BankLedgerItemDto
    {
        public Guid LineId { get; set; }
        public Guid EntryId { get; set; }
        public string EntryNumber { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? ReferenceType { get; set; }
        public decimal Amount { get; set; }
    }

    public class BankReconciliationDto
    {
        public Guid BankStatementId { get; set; }
        public Guid BankAccountId { get; set; }
        public DateTime StatementDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
        public Guid? GlAccountId { get; set; }
        public string? GlAccountNumber { get; set; }

        /// <summary>The balance of the bank's ledger account at the statement date.</summary>
        public decimal LedgerBalance { get; set; }

        /// <summary>Ledger lines the bank has not shown yet (deposits in transit, cheques not cashed): debit − credit.</summary>
        public decimal OutstandingLedgerNet { get; set; }

        /// <summary>Statement lines the books do not have: they have to be matched or posted.</summary>
        public decimal UnmatchedStatementNet { get; set; }

        /// <summary>Ledger balance − outstanding + unmatched statement lines: what the statement balance should be.</summary>
        public decimal ExpectedClosing { get; set; }

        /// <summary>The statement's closing balance − <see cref="ExpectedClosing"/>; zero when everything is explained.</summary>
        public decimal Difference { get; set; }
        public int UnmatchedStatementLines { get; set; }
        public bool CanReconcile { get; set; }
        public List<BankStatementLineDto> Lines { get; set; } = new();

        /// <summary>Ledger lines still free to be matched: the outstanding ones, and those a few days after the statement date.</summary>
        public List<BankLedgerItemDto> UnmatchedLedger { get; set; } = new();
        public List<string> Notes { get; set; } = new();
    }

    public class ImportBankStatementLinesResult
    {
        public int Imported { get; set; }
        public List<StatementRowError> Errors { get; set; } = new();
    }

    /// <summary>Adds the lines of a bank's CSV export to a statement. Nothing is imported when any row is wrong or already on the statement.</summary>
    public class ImportBankStatementLinesCommand : IRequest<ImportBankStatementLinesResult>
    {
        public Guid BankStatementId { get; set; }
        public string Csv { get; set; } = string.Empty;
    }

    public class AddBankStatementLineCommand : IRequest<BankStatementLineDto>
    {
        public Guid BankStatementId { get; set; }
        public DateTime LineDate { get; set; }
        public string? Description { get; set; }
        public string? Reference { get; set; }
        public decimal Amount { get; set; }
    }

    public class RemoveBankStatementLineCommand : IRequest<Unit>
    {
        public Guid LineId { get; set; }
    }

    public class GetBankReconciliationQuery : IRequest<BankReconciliationDto>
    {
        public Guid BankStatementId { get; set; }
    }

    public class AutoMatchBankStatementCommand : IRequest<BankReconciliationDto>
    {
        public Guid BankStatementId { get; set; }
        public int ToleranceDays { get; set; } = BankMatcher.DefaultToleranceDays;
    }

    public class MatchBankStatementLineCommand : IRequest<BankReconciliationDto>
    {
        public Guid LineId { get; set; }
        public Guid JournalLineId { get; set; }
    }

    public class UnmatchBankStatementLineCommand : IRequest<BankReconciliationDto>
    {
        public Guid LineId { get; set; }
    }

    internal static class BankReconciliationSupport
    {
        public const int CandidateWindowDays = 10;

        public static async Task<(BankStatement Statement, BankAccount Account)> LoadAsync(IAccountingUnitOfWork uow, Guid statementId, CancellationToken ct)
        {
            var statement = await uow.BankStatements.GetByIdAsync(statementId, ct)
                ?? throw new KeyNotFoundException($"Bank statement {statementId} not found.");
            var account = await uow.BankAccounts.GetByIdAsync(statement.BankAccountId, ct)
                ?? throw new KeyNotFoundException($"Bank account {statement.BankAccountId} not found.");
            return (statement, account);
        }

        public static BankStatementLineDto Map(BankStatementLine l, IReadOnlyDictionary<Guid, LedgerRow>? ledger = null) => new()
        {
            Id = l.Id,
            BankStatementId = l.BankStatementId,
            LineDate = l.LineDate,
            Description = l.Description,
            Reference = l.Reference,
            Amount = l.Amount,
            Balance = l.Balance,
            MatchStatus = l.MatchStatus switch { BankStatementLine.Matched => "Matched", BankStatementLine.Posted => "Posted", _ => "Unmatched" },
            MatchedJournalLineId = l.MatchedJournalLineId,
            JournalEntryId = l.JournalEntryId,
            MatchedEntryNumber = l.MatchedJournalLineId.HasValue && ledger != null && ledger.TryGetValue(l.MatchedJournalLineId.Value, out var row) ? row.EntryNumber : null,
        };

        /// <summary>The ledger lines of the bank's account up to a few days after the statement date, oldest first.</summary>
        public static async Task<List<LedgerRow>> LedgerAsync(IAccountingUnitOfWork uow, BankAccount account, DateTime statementDate, CancellationToken ct) =>
            account.GlAccountId.HasValue
                ? await uow.JournalLines.GetLedgerLinesAsync(new[] { account.GlAccountId.Value }, null, statementDate.Date.AddDays(CandidateWindowDays), ct)
                : new List<LedgerRow>();

        public static async Task<BankReconciliationDto> BuildAsync(IAccountingUnitOfWork uow, BankStatement statement, BankAccount account, CancellationToken ct)
        {
            var date = statement.StatementDate.Date;
            var dto = new BankReconciliationDto
            {
                BankStatementId = statement.Id,
                BankAccountId = account.Id,
                StatementDate = date,
                Status = ((BankStatementStatus)statement.Status).ToString(),
                OpeningBalance = statement.OpeningBalance,
                ClosingBalance = statement.ClosingBalance,
                GlAccountId = account.GlAccountId,
            };

            var thisStatement = await uow.BankStatementLines.ListByStatementAsync(statement.Id, ct);
            var ledger = await LedgerAsync(uow, account, date, ct);
            var byId = ledger.ToDictionary(r => r.LineId);
            dto.Lines = thisStatement.Select(l => Map(l, byId)).ToList();
            dto.UnmatchedStatementLines = thisStatement.Count(l => l.MatchStatus == BankStatementLine.Unmatched);

            if (!account.GlAccountId.HasValue)
            {
                dto.Notes.Add("The bank account has no ledger account: link it to a cash / bank account before reconciling.");
                return dto;
            }
            var gl = await uow.Accounts.GetByIdAsync(account.GlAccountId.Value, ct);
            dto.GlAccountNumber = gl?.AccountNumber;

            var accountLines = await uow.BankStatementLines.ListByBankAccountAsync(account.Id, date, ct);
            var matchedAnywhere = await uow.BankStatementLines.MatchedJournalLineIdsAsync(ct);
            var matchedToThisAccount = accountLines.Where(l => l.MatchedJournalLineId.HasValue).Select(l => l.MatchedJournalLineId!.Value).ToHashSet();

            // items before the first statement line of the account are taken as cleared by the opening balance
            var start = accountLines.Count > 0 ? accountLines.Min(l => l.LineDate) : date;
            var atDate = ledger.Where(r => r.EntryDate.Date <= date || matchedToThisAccount.Contains(r.LineId)).ToList();
            dto.LedgerBalance = atDate.Sum(r => r.Debit - r.Credit);
            dto.OutstandingLedgerNet = atDate.Where(r => r.EntryDate.Date >= start && !matchedAnywhere.Contains(r.LineId)).Sum(r => r.Debit - r.Credit);
            dto.UnmatchedStatementNet = accountLines.Where(l => l.MatchStatus == BankStatementLine.Unmatched).Sum(l => l.Amount);
            dto.ExpectedClosing = dto.LedgerBalance - dto.OutstandingLedgerNet + dto.UnmatchedStatementNet;
            dto.Difference = statement.ClosingBalance - dto.ExpectedClosing;

            dto.UnmatchedLedger = ledger.Where(r => !matchedAnywhere.Contains(r.LineId) && (r.EntryDate.Date >= start || r.EntryDate.Date > date.AddDays(-CandidateWindowDays)))
                .Select(r => new BankLedgerItemDto
                {
                    LineId = r.LineId,
                    EntryId = r.EntryId,
                    EntryNumber = r.EntryNumber,
                    EntryDate = r.EntryDate,
                    Description = string.IsNullOrWhiteSpace(r.LineDescription) ? r.EntryDescription : r.LineDescription,
                    ReferenceType = r.ReferenceType,
                    Amount = r.Debit - r.Credit,
                }).ToList();

            if (accountLines.Count > 0)
                dto.Notes.Add($"Ledger lines before {start:yyyy-MM-dd} (the first statement line) are taken as cleared by the opening balance.");
            var lineSum = thisStatement.Sum(l => l.Amount);
            if (thisStatement.Count > 0 && Math.Abs(statement.OpeningBalance + lineSum - statement.ClosingBalance) > 0.005m)
                dto.Notes.Add($"The lines add up to {lineSum:N2}, but the statement moves from {statement.OpeningBalance:N2} to {statement.ClosingBalance:N2}: a line is missing or wrong.");
            dto.CanReconcile = thisStatement.Count > 0 && dto.UnmatchedStatementLines == 0 && Math.Abs(dto.Difference) <= 0.005m && statement.Status == (int)BankStatementStatus.Open;
            return dto;
        }
    }

    public class ImportBankStatementLinesCommandHandler : IRequestHandler<ImportBankStatementLinesCommand, ImportBankStatementLinesResult>
    {
        private readonly IAccountingUnitOfWork _uow;

        public ImportBankStatementLinesCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<ImportBankStatementLinesResult> Handle(ImportBankStatementLinesCommand request, CancellationToken ct)
        {
            var (statement, account) = await BankReconciliationSupport.LoadAsync(_uow, request.BankStatementId, ct);
            if (statement.Status == (int)BankStatementStatus.Reconciled)
                throw new BusinessRuleException("Accounting", "The statement is already reconciled: its lines cannot change.");

            var parsed = BankCsvParser.Parse(request.Csv);
            var result = new ImportBankStatementLinesResult { Errors = parsed.Errors.ToList() };

            // the same movement twice (date, amount, reference), in the file or already on the account, is refused rather than silently doubled
            var existing = await _uow.BankStatementLines.ListByBankAccountAsync(account.Id, DateTime.MaxValue.Date, ct);
            var seen = existing.Select(l => Key(l.LineDate, l.Amount, l.Reference)).ToHashSet();
            foreach (var row in parsed.Rows)
                if (!seen.Add(Key(row.Date, row.Amount, row.Reference)))
                    result.Errors.Add(new StatementRowError(row.RowNumber, $"{row.Date:yyyy-MM-dd} {row.Amount:N2}{(row.Reference == null ? "" : " (" + row.Reference + ")")} is already on this account's statements."));

            if (result.Errors.Count > 0)
                return result;

            await _uow.BankStatementLines.AddRangeAsync(parsed.Rows.Select(r =>
                BankStatementLine.Create(statement.Id, account.Id, r.Date, r.Description, r.Reference, r.Amount, r.Balance)), ct);
            await _uow.SaveChangesAsync(ct);
            result.Imported = parsed.Rows.Count;
            return result;
        }

        private static string Key(DateTime date, decimal amount, string? reference) => $"{date:yyyyMMdd}|{amount:0.00}|{reference?.Trim().ToUpperInvariant()}";
    }

    public class AddBankStatementLineCommandHandler : IRequestHandler<AddBankStatementLineCommand, BankStatementLineDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public AddBankStatementLineCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<BankStatementLineDto> Handle(AddBankStatementLineCommand request, CancellationToken ct)
        {
            var (statement, account) = await BankReconciliationSupport.LoadAsync(_uow, request.BankStatementId, ct);
            if (statement.Status == (int)BankStatementStatus.Reconciled)
                throw new BusinessRuleException("Accounting", "The statement is already reconciled: its lines cannot change.");
            if (request.Amount == 0m)
                throw new BusinessRuleException("Accounting", "A statement line moves money: the amount cannot be zero.");

            var line = BankStatementLine.Create(statement.Id, account.Id, request.LineDate, request.Description, request.Reference, request.Amount);
            await _uow.BankStatementLines.AddRangeAsync(new[] { line }, ct);
            await _uow.SaveChangesAsync(ct);
            return BankReconciliationSupport.Map(line);
        }
    }

    public class RemoveBankStatementLineCommandHandler : IRequestHandler<RemoveBankStatementLineCommand, Unit>
    {
        private readonly IAccountingUnitOfWork _uow;

        public RemoveBankStatementLineCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<Unit> Handle(RemoveBankStatementLineCommand request, CancellationToken ct)
        {
            var line = await _uow.BankStatementLines.GetByIdAsync(request.LineId, ct)
                ?? throw new KeyNotFoundException($"Statement line {request.LineId} not found.");
            if (line.MatchStatus != BankStatementLine.Unmatched)
                throw new BusinessRuleException("Accounting", "Unmatch the line before removing it (a line posted as an entry cannot be removed: reverse that entry).");
            var statement = await _uow.BankStatements.GetByIdAsync(line.BankStatementId, ct);
            if (statement is { Status: (int)BankStatementStatus.Reconciled })
                throw new BusinessRuleException("Accounting", "The statement is already reconciled: its lines cannot change.");

            _uow.BankStatementLines.Remove(line);
            await _uow.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }

    public class GetBankReconciliationQueryHandler : IRequestHandler<GetBankReconciliationQuery, BankReconciliationDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public GetBankReconciliationQueryHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<BankReconciliationDto> Handle(GetBankReconciliationQuery request, CancellationToken ct)
        {
            var (statement, account) = await BankReconciliationSupport.LoadAsync(_uow, request.BankStatementId, ct);
            return await BankReconciliationSupport.BuildAsync(_uow, statement, account, ct);
        }
    }

    public class AutoMatchBankStatementCommandHandler : IRequestHandler<AutoMatchBankStatementCommand, BankReconciliationDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public AutoMatchBankStatementCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<BankReconciliationDto> Handle(AutoMatchBankStatementCommand request, CancellationToken ct)
        {
            var (statement, account) = await BankReconciliationSupport.LoadAsync(_uow, request.BankStatementId, ct);
            if (statement.Status == (int)BankStatementStatus.Reconciled)
                throw new BusinessRuleException("Accounting", "The statement is already reconciled.");
            if (!account.GlAccountId.HasValue)
                throw new BusinessRuleException("Accounting", "The bank account has no ledger account: link it before matching.");
            if (request.ToleranceDays < 0 || request.ToleranceDays > BankReconciliationSupport.CandidateWindowDays)
                throw new BusinessRuleException("Accounting", $"The date tolerance is 0 to {BankReconciliationSupport.CandidateWindowDays} days.");

            var lines = await _uow.BankStatementLines.ListByStatementAsync(statement.Id, ct);
            var matched = await _uow.BankStatementLines.MatchedJournalLineIdsAsync(ct);
            var ledger = (await BankReconciliationSupport.LedgerAsync(_uow, account, statement.StatementDate, ct)).Where(r => !matched.Contains(r.LineId)).ToList();

            foreach (var match in BankMatcher.Match(lines, ledger, request.ToleranceDays))
                lines.First(l => l.Id == match.StatementLineId).MatchTo(match.JournalLineId);
            await _uow.SaveChangesAsync(ct);
            return await BankReconciliationSupport.BuildAsync(_uow, statement, account, ct);
        }
    }

    public class MatchBankStatementLineCommandHandler : IRequestHandler<MatchBankStatementLineCommand, BankReconciliationDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public MatchBankStatementLineCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<BankReconciliationDto> Handle(MatchBankStatementLineCommand request, CancellationToken ct)
        {
            var line = await _uow.BankStatementLines.GetByIdAsync(request.LineId, ct)
                ?? throw new KeyNotFoundException($"Statement line {request.LineId} not found.");
            var (statement, account) = await BankReconciliationSupport.LoadAsync(_uow, line.BankStatementId, ct);
            if (statement.Status == (int)BankStatementStatus.Reconciled)
                throw new BusinessRuleException("Accounting", "The statement is already reconciled.");
            if (line.MatchStatus != BankStatementLine.Unmatched)
                throw new BusinessRuleException("Accounting", "The statement line is already matched: unmatch it first.");

            var ledger = await BankReconciliationSupport.LedgerAsync(_uow, account, statement.StatementDate, ct);
            var row = ledger.FirstOrDefault(r => r.LineId == request.JournalLineId)
                ?? throw new BusinessRuleException("Accounting", "That ledger line is not on the bank's ledger account (or is dated too long after the statement).");
            if ((await _uow.BankStatementLines.MatchedJournalLineIdsAsync(ct)).Contains(row.LineId))
                throw new BusinessRuleException("Accounting", $"Ledger line of entry {row.EntryNumber} is already matched to another statement line.");
            if (row.Debit - row.Credit != line.Amount)
                throw new BusinessRuleException("Accounting", $"The amounts differ: the statement line is {line.Amount:N2}, the ledger line {row.Debit - row.Credit:N2}.");

            line.MatchTo(row.LineId);
            await _uow.SaveChangesAsync(ct);
            return await BankReconciliationSupport.BuildAsync(_uow, statement, account, ct);
        }
    }

    public class UnmatchBankStatementLineCommandHandler : IRequestHandler<UnmatchBankStatementLineCommand, BankReconciliationDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public UnmatchBankStatementLineCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<BankReconciliationDto> Handle(UnmatchBankStatementLineCommand request, CancellationToken ct)
        {
            var line = await _uow.BankStatementLines.GetByIdAsync(request.LineId, ct)
                ?? throw new KeyNotFoundException($"Statement line {request.LineId} not found.");
            var (statement, account) = await BankReconciliationSupport.LoadAsync(_uow, line.BankStatementId, ct);
            if (statement.Status == (int)BankStatementStatus.Reconciled)
                throw new BusinessRuleException("Accounting", "The statement is already reconciled.");
            try
            {
                line.Unmatch();
            }
            catch (InvalidOperationException ex)
            {
                throw new BusinessRuleException("Accounting", ex.Message);
            }
            await _uow.SaveChangesAsync(ct);
            return await BankReconciliationSupport.BuildAsync(_uow, statement, account, ct);
        }
    }
}
