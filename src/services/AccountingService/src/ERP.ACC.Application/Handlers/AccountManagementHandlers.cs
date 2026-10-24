using ERP.ACC.Application.Commands;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Queries;
using ERP.ACC.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.ACC.Application.Handlers
{
    /// <summary>Rules shared by account management and the posting paths (ROADMAP P1-2).</summary>
    internal static class AccountRules
    {
        /// <summary>Ids of the accounts that have an active child: they group accounts and take no postings.</summary>
        public static async Task<HashSet<Guid>> ParentIdsAsync(IAccountRepository accounts, CancellationToken ct) =>
            await accounts.GetParentAccountIdsAsync(ct) ?? new HashSet<Guid>();

        public static void EnsurePostable(Account account, HashSet<Guid> parentIds)
        {
            if (!account.IsActive)
                throw new BusinessRuleException("Accounting", $"Account {account.AccountNumber} is inactive and takes no postings.");
            if (parentIds.Contains(account.Id))
                throw new BusinessRuleException("Accounting", $"Account {account.AccountNumber} groups other accounts and takes no postings; use one of its sub-accounts.");
        }
    }

    public class UpdateAccountCommandHandler : IRequestHandler<UpdateAccountCommand, AccountDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public UpdateAccountCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<AccountDto> Handle(UpdateAccountCommand request, CancellationToken cancellationToken)
        {
            var all = await _uow.Accounts.GetAllTrackedAsync(cancellationToken);
            var byId = all.ToDictionary(a => a.Id);
            var account = byId.GetValueOrDefault(request.AccountId)
                ?? throw new KeyNotFoundException($"Account {request.AccountId} not found.");

            try
            {
                account.Rename(request.Name);

                if (request.ParentAccountId != account.ParentAccountId)
                {
                    if (request.ParentAccountId is not { } parentId)
                    {
                        account.MakeTopLevel();
                    }
                    else
                    {
                        var parent = byId.GetValueOrDefault(parentId)
                            ?? throw new BusinessRuleException("Accounting", $"Parent account {parentId} does not exist.");
                        if (Account.WouldCreateCycle(account, parent, byId))
                            throw new BusinessRuleException("Accounting", "An account cannot be moved under itself or one of its own sub-accounts.");
                        account.AttachTo(parent);
                    }

                    Account.RecalculateLevels(all);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                throw new BusinessRuleException("Accounting", ex.Message);
            }

            await _uow.SaveChangesAsync(cancellationToken);
            return CreateAccountCommandHandler.MapAccountToDto(account);
        }
    }

    public class SetAccountActiveCommandHandler : IRequestHandler<SetAccountActiveCommand, AccountDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public SetAccountActiveCommandHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<AccountDto> Handle(SetAccountActiveCommand request, CancellationToken cancellationToken)
        {
            var all = await _uow.Accounts.GetAllTrackedAsync(cancellationToken);
            var account = all.FirstOrDefault(a => a.Id == request.AccountId)
                ?? throw new KeyNotFoundException($"Account {request.AccountId} not found.");

            if (request.Active)
            {
                account.Activate();
            }
            else if (account.IsActive)
            {
                if (all.Any(a => a.IsActive && a.ParentAccountId == account.Id))
                    throw new BusinessRuleException("Accounting", $"Account {account.AccountNumber} has active sub-accounts; deactivate them first.");

                var mappings = await _uow.PostingMappings.GetAllAsync(cancellationToken);
                if (mappings.Any(m => m.AccountId == account.Id))
                    throw new BusinessRuleException("Accounting",
                        $"Account {account.AccountNumber} is used by the posting profile ({string.Join(", ", mappings.Where(m => m.AccountId == account.Id).Select(m => m.PostingKey))}); map those keys to another account first.");

                var balances = await _uow.JournalLines.GetAccountBalancesAsync(null, null, null, cancellationToken);
                var balance = balances.FirstOrDefault(b => b.AccountId == account.Id);
                if (balance != null && balance.TotalDebit != balance.TotalCredit)
                    throw new BusinessRuleException("Accounting", $"Account {account.AccountNumber} still has a balance ({balance.TotalDebit - balance.TotalCredit:N2}); clear it before deactivating.");

                account.Deactivate();
            }

            await _uow.SaveChangesAsync(cancellationToken);
            return CreateAccountCommandHandler.MapAccountToDto(account);
        }
    }

    public class GetAccountTreeQueryHandler : IRequestHandler<GetAccountTreeQuery, List<AccountTreeNodeDto>>
    {
        private readonly IAccountRepository _accounts;

        public GetAccountTreeQueryHandler(IAccountRepository accounts) => _accounts = accounts;

        public async Task<List<AccountTreeNodeDto>> Handle(GetAccountTreeQuery request, CancellationToken cancellationToken)
        {
            var all = await _accounts.GetAllAsync(cancellationToken);
            var parentIds = new HashSet<Guid>(all.Where(a => a.IsActive && a.ParentAccountId.HasValue).Select(a => a.ParentAccountId!.Value));
            var known = all.Select(a => a.Id).ToHashSet();
            var childrenOf = all.Where(a => a.ParentAccountId.HasValue && known.Contains(a.ParentAccountId.Value))
                .ToLookup(a => a.ParentAccountId!.Value);

            AccountTreeNodeDto Build(Account a) => new()
            {
                Account = CreateAccountCommandHandler.MapAccountToDto(a, parentIds.Contains(a.Id)),
                Children = childrenOf[a.Id].OrderBy(c => c.AccountNumber).Select(Build).ToList(),
            };

            return all.Where(a => !a.ParentAccountId.HasValue || !known.Contains(a.ParentAccountId.Value))
                .OrderBy(a => a.AccountNumber).Select(Build).ToList();
        }
    }
}
