using ERP.ACC.Application.Bank;
using ERP.ACC.Application.Commands;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Queries;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.ACC.Application.Handlers
{
    public class CreateBankAccountCommandHandler : IRequestHandler<CreateBankAccountCommand, BankAccountDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public CreateBankAccountCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<BankAccountDto> Handle(CreateBankAccountCommand request, CancellationToken cancellationToken)
        {
            var ba = BankAccount.Create(
                request.AccountName,
                request.BankName,
                request.AccountNumber,
                request.OpeningBalance,
                request.CurrencyCode,
                request.GlAccountId);

            await _uow.BankAccounts.AddAsync(ba, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return MapToDto(ba);
        }

        internal static BankAccountDto MapToDto(BankAccount b) => new()
        {
            Id = b.Id,
            AccountName = b.AccountName,
            BankName = b.BankName,
            AccountNumber = b.AccountNumber,
            CurrencyCode = b.CurrencyCode,
            CurrentBalance = b.CurrentBalance,
            IsActive = b.IsActive,
            GlAccountId = b.GlAccountId,
            CreatedAt = b.CreatedAt
        };
    }

    public class ListBankAccountsQueryHandler : IRequestHandler<ListBankAccountsQuery, List<BankAccountDto>>
    {
        private readonly IBankAccountRepository _bankAccounts;

        public ListBankAccountsQueryHandler(IBankAccountRepository bankAccounts) => _bankAccounts = bankAccounts;

        public async Task<List<BankAccountDto>> Handle(ListBankAccountsQuery request, CancellationToken cancellationToken)
        {
            var accounts = await _bankAccounts.GetAllAsync(request.ActiveOnly, cancellationToken);
            return accounts.Select(CreateBankAccountCommandHandler.MapToDto).ToList();
        }
    }

    public class GetBankAccountQueryHandler : IRequestHandler<GetBankAccountQuery, BankAccountDto?>
    {
        private readonly IBankAccountRepository _bankAccounts;

        public GetBankAccountQueryHandler(IBankAccountRepository bankAccounts) => _bankAccounts = bankAccounts;

        public async Task<BankAccountDto?> Handle(GetBankAccountQuery request, CancellationToken cancellationToken)
        {
            var ba = await _bankAccounts.GetByIdAsync(request.BankAccountId, cancellationToken);
            return ba == null ? null : CreateBankAccountCommandHandler.MapToDto(ba);
        }
    }

    public class CreateBankStatementCommandHandler : IRequestHandler<CreateBankStatementCommand, BankStatementDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public CreateBankStatementCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<BankStatementDto> Handle(CreateBankStatementCommand request, CancellationToken cancellationToken)
        {
            var bankAccount = await _uow.BankAccounts.GetByIdAsync(request.BankAccountId, cancellationToken)
                ?? throw new InvalidOperationException($"Bank account {request.BankAccountId} not found.");

            var statement = BankStatement.Create(
                request.BankAccountId,
                request.StatementDate,
                request.OpeningBalance,
                request.ClosingBalance,
                request.Notes);

            await _uow.BankStatements.AddAsync(statement, cancellationToken);

            bankAccount.UpdateBalance(request.ClosingBalance);
            await _uow.BankAccounts.UpdateAsync(bankAccount, cancellationToken);

            await _uow.SaveChangesAsync(cancellationToken);

            return MapToDto(statement);
        }

        internal static BankStatementDto MapToDto(BankStatement s) => new()
        {
            Id = s.Id,
            BankAccountId = s.BankAccountId,
            StatementDate = s.StatementDate,
            OpeningBalance = s.OpeningBalance,
            ClosingBalance = s.ClosingBalance,
            Status = ((BankStatementStatus)s.Status).ToString(),
            ReconciledAt = s.ReconciledAt,
            Notes = s.Notes,
            CreatedAt = s.CreatedAt
        };
    }

    public class ReconcileBankStatementCommandHandler : IRequestHandler<ReconcileBankStatementCommand, BankStatementDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public ReconcileBankStatementCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<BankStatementDto> Handle(ReconcileBankStatementCommand request, CancellationToken cancellationToken)
        {
            var statement = await _uow.BankStatements.GetByIdAsync(request.BankStatementId, cancellationToken)
                ?? throw new InvalidOperationException($"Bank statement {request.BankStatementId} not found.");

            // A statement with lines is reconciled line by line (ROADMAP P1-4): every line matched or posted and the books explain the closing balance. One typed without lines
            // keeps the old behaviour (a status flag), since there is nothing to compare.
            var lines = await _uow.BankStatementLines.ListByStatementAsync(statement.Id, cancellationToken);
            if (lines.Count > 0)
            {
                var account = await _uow.BankAccounts.GetByIdAsync(statement.BankAccountId, cancellationToken)
                    ?? throw new KeyNotFoundException($"Bank account {statement.BankAccountId} not found.");
                var recon = await BankReconciliationSupport.BuildAsync(_uow, statement, account, cancellationToken);
                if (!account.GlAccountId.HasValue)
                    throw new BusinessRuleException("Accounting", "The bank account has no ledger account: link it before reconciling.");
                if (recon.UnmatchedStatementLines > 0)
                    throw new BusinessRuleException("Accounting", $"{recon.UnmatchedStatementLines} statement line(s) are not matched to the books: match them or post them first.");
                if (Math.Abs(recon.Difference) > 0.005m)
                    throw new BusinessRuleException("Accounting", $"The books do not explain the closing balance: the difference is {recon.Difference:N2}.");
            }

            try
            {
                statement.Reconcile(request.Notes);
            }
            catch (InvalidOperationException ex)
            {
                throw new BusinessRuleException("Accounting", ex.Message);
            }
            await _uow.BankStatements.UpdateAsync(statement, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return CreateBankStatementCommandHandler.MapToDto(statement);
        }
    }

    public class ListBankStatementsQueryHandler : IRequestHandler<ListBankStatementsQuery, List<BankStatementDto>>
    {
        private readonly IBankStatementRepository _statements;

        public ListBankStatementsQueryHandler(IBankStatementRepository statements) => _statements = statements;

        public async Task<List<BankStatementDto>> Handle(ListBankStatementsQuery request, CancellationToken cancellationToken)
        {
            var statements = await _statements.GetByBankAccountIdAsync(request.BankAccountId, cancellationToken);
            return statements.Select(CreateBankStatementCommandHandler.MapToDto).ToList();
        }
    }
}
