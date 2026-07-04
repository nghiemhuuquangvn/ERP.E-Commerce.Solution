using ERP.ACC.Application.Commands;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Queries;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using MediatR;

namespace ERP.ACC.Application.Handlers
{
    public class CreateBankAccountCommandHandler : IRequestHandler<CreateBankAccountCommand, BankAccountDto>
    {
        private readonly IBankAccountRepository _bankAccounts;

        public CreateBankAccountCommandHandler(IBankAccountRepository bankAccounts) => _bankAccounts = bankAccounts;

        public async Task<BankAccountDto> Handle(CreateBankAccountCommand request, CancellationToken cancellationToken)
        {
            var ba = BankAccount.Create(
                request.AccountName,
                request.BankName,
                request.AccountNumber,
                request.OpeningBalance,
                request.CurrencyCode,
                request.GlAccountId);

            await _bankAccounts.AddAsync(ba, cancellationToken);
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
        private readonly IBankStatementRepository _statements;
        private readonly IBankAccountRepository _bankAccounts;

        public CreateBankStatementCommandHandler(IBankStatementRepository statements, IBankAccountRepository bankAccounts)
        {
            _statements = statements;
            _bankAccounts = bankAccounts;
        }

        public async Task<BankStatementDto> Handle(CreateBankStatementCommand request, CancellationToken cancellationToken)
        {
            var bankAccount = await _bankAccounts.GetByIdAsync(request.BankAccountId, cancellationToken)
                ?? throw new InvalidOperationException($"Bank account {request.BankAccountId} not found.");

            var statement = BankStatement.Create(
                request.BankAccountId,
                request.StatementDate,
                request.OpeningBalance,
                request.ClosingBalance,
                request.Notes);

            await _statements.AddAsync(statement, cancellationToken);

            bankAccount.UpdateBalance(request.ClosingBalance);
            await _bankAccounts.UpdateAsync(bankAccount, cancellationToken);

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
        private readonly IBankStatementRepository _statements;

        public ReconcileBankStatementCommandHandler(IBankStatementRepository statements) => _statements = statements;

        public async Task<BankStatementDto> Handle(ReconcileBankStatementCommand request, CancellationToken cancellationToken)
        {
            var statement = await _statements.GetByIdAsync(request.BankStatementId, cancellationToken)
                ?? throw new InvalidOperationException($"Bank statement {request.BankStatementId} not found.");

            statement.Reconcile(request.Notes);
            await _statements.UpdateAsync(statement, cancellationToken);
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
