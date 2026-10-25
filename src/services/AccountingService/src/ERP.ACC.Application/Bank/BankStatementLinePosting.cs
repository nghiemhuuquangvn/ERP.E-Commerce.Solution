using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Application.Bank
{
    /// <summary>
    /// Books a statement line the ledger does not have (a bank fee, the interest on a deposit, a direct debit) as a journal entry against the bank's ledger account and the counter-account,
    /// and marks the line as posted (ROADMAP P1-4). The counter-account is a posting key (<see cref="PostingKeys.BankFee"/> for a withdrawal, <see cref="PostingKeys.BankInterestIncome"/> for a deposit by default)
    /// or an account chosen by the user.
    /// </summary>
    public class PostBankStatementLineCommand : IRequest<BankReconciliationDto>
    {
        public Guid LineId { get; set; }

        /// <summary>One of the posting keys; empty = the default for the direction of the line. Ignored when <see cref="AccountId"/> is given.</summary>
        public string? PostingKey { get; set; }
        public Guid? AccountId { get; set; }
        public string? Description { get; set; }
    }

    public class PostBankStatementLineCommandHandler : IRequestHandler<PostBankStatementLineCommand, BankReconciliationDto>
    {
        /// <summary>Keys a person may pick for the other side of the entry (the bank's own account is never one of them).</summary>
        public static readonly string[] AllowedKeys =
        {
            PostingKeys.BankFee, PostingKeys.BankInterestIncome, PostingKeys.InterestExpense, PostingKeys.FxGain, PostingKeys.FxLoss,
            PostingKeys.AccountsReceivable, PostingKeys.AccountsPayable, PostingKeys.SalesRevenue, PostingKeys.CashOverShort,
        };

        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;
        private readonly ILogger<PostBankStatementLineCommandHandler> _logger;

        public PostBankStatementLineCommandHandler(IAccountingUnitOfWork uow, IJournalPostingService posting, ILogger<PostBankStatementLineCommandHandler> logger)
        {
            _uow = uow;
            _posting = posting;
            _logger = logger;
        }

        public async Task<BankReconciliationDto> Handle(PostBankStatementLineCommand request, CancellationToken ct)
        {
            var line = await _uow.BankStatementLines.GetByIdAsync(request.LineId, ct)
                ?? throw new KeyNotFoundException($"Statement line {request.LineId} not found.");
            var (statement, account) = await BankReconciliationSupport.LoadAsync(_uow, line.BankStatementId, ct);
            if (statement.Status == (int)BankStatementStatus.Reconciled)
                throw new BusinessRuleException("Accounting", "The statement is already reconciled.");
            if (line.MatchStatus != BankStatementLine.Unmatched)
                throw new BusinessRuleException("Accounting", "The statement line is already matched or posted.");
            if (!account.GlAccountId.HasValue)
                throw new BusinessRuleException("Accounting", "The bank account has no ledger account: link it before posting its lines.");

            Guid? counterAccount = request.AccountId;
            string? counterKey = null;
            if (counterAccount.HasValue)
            {
                if (counterAccount == account.GlAccountId)
                    throw new BusinessRuleException("Accounting", "The other side of the entry cannot be the bank's own ledger account.");
            }
            else
            {
                counterKey = string.IsNullOrWhiteSpace(request.PostingKey)
                    ? (line.Amount < 0m ? PostingKeys.BankFee : PostingKeys.BankInterestIncome)
                    : request.PostingKey.Trim();
                if (!AllowedKeys.Contains(counterKey))
                    throw new BusinessRuleException("Accounting", $"'{counterKey}' is not an account for a bank statement line: choose one of {string.Join(", ", AllowedKeys)} or an account.");
            }

            var description = string.IsNullOrWhiteSpace(request.Description) ? (line.Description ?? "Bank statement line") : request.Description.Trim();
            var posting = PostingRules.ForBankStatementLine(line.Id, line.LineDate, line.Amount, account.GlAccountId.Value, counterKey, counterAccount, description);
            var entry = await _posting.TryPostAsync(_logger, posting, ct);
            if (entry == null)
                throw new BusinessRuleException("Accounting", "The line could not be posted: complete the posting profile (the key for the other side of the entry).");

            var bankLine = entry.Lines.FirstOrDefault(l => l.AccountId == account.GlAccountId.Value)
                ?? throw new BusinessRuleException("Accounting", "The entry has no line on the bank's ledger account.");
            line.MarkPosted(entry.Id, bankLine.Id);
            await _uow.SaveChangesAsync(ct);
            return await BankReconciliationSupport.BuildAsync(_uow, statement, account, ct);
        }
    }
}
