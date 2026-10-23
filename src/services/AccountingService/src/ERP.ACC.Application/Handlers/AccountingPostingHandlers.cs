using ERP.ACC.Application.Commands;
using ERP.ACC.Application.DTOs;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Application.Queries;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.ACC.Application.Handlers
{
    public class GetPostingProfileQueryHandler : IRequestHandler<GetPostingProfileQuery, PostingProfileDto>
    {
        private readonly IAccountingUnitOfWork _uow;

        public GetPostingProfileQueryHandler(IAccountingUnitOfWork uow) => _uow = uow;

        public async Task<PostingProfileDto> Handle(GetPostingProfileQuery request, CancellationToken ct) =>
            await PostingProfileMapper.LoadAsync(_uow, ct);
    }

    /// <summary>Points posting keys at accounts. Every account must exist and be active; AR and AP must stay different accounts.</summary>
    public class UpdatePostingProfileCommandHandler : IRequestHandler<UpdatePostingProfileCommand, PostingProfileDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly ITenantProvider _tenant;

        public UpdatePostingProfileCommandHandler(IAccountingUnitOfWork uow, ITenantProvider tenant)
        {
            _uow = uow;
            _tenant = tenant;
        }

        public async Task<PostingProfileDto> Handle(UpdatePostingProfileCommand request, CancellationToken ct)
        {
            var tenantId = _tenant.CurrentTenantId ?? throw new BusinessRuleException("Accounting", "A tenant is required.");
            var inputs = request.Mappings;

            var unknown = inputs.Select(i => i.PostingKey).Where(k => !PostingKeys.All.Contains(k)).Distinct().ToList();
            if (unknown.Count > 0)
                throw new BusinessRuleException("Accounting", $"Unknown posting key(s): {string.Join(", ", unknown)}.");
            if (inputs.GroupBy(i => i.PostingKey).Any(g => g.Count() > 1))
                throw new BusinessRuleException("Accounting", "A posting key can only be mapped once.");

            var accounts = await _uow.Accounts.GetByIdsAsync(inputs.Select(i => i.AccountId).Distinct(), ct);
            foreach (var input in inputs)
            {
                var account = accounts.FirstOrDefault(a => a.Id == input.AccountId)
                    ?? throw new BusinessRuleException("Accounting", $"Account {input.AccountId} does not exist.");
                if (!account.IsActive)
                    throw new BusinessRuleException("Accounting", $"Account {account.AccountNumber} is inactive.");
            }

            var current = await _uow.PostingMappings.GetAllAsync(ct);
            var resulting = current.ToDictionary(m => m.PostingKey, m => m.AccountId);
            foreach (var input in inputs)
                resulting[input.PostingKey] = input.AccountId;
            if (resulting.TryGetValue(PostingKeys.AccountsReceivable, out var ar)
                && resulting.TryGetValue(PostingKeys.AccountsPayable, out var ap) && ar == ap)
                throw new BusinessRuleException("Accounting", "Accounts receivable and accounts payable cannot be the same account.");

            foreach (var input in inputs)
            {
                var existing = current.FirstOrDefault(m => m.PostingKey == input.PostingKey);
                if (existing != null)
                    existing.PointTo(input.AccountId);
                else
                    await _uow.PostingMappings.AddAsync(PostingAccountMapping.Create(tenantId, input.PostingKey, input.AccountId), ct);
            }

            await _uow.SaveChangesAsync(ct);
            return await PostingProfileMapper.LoadAsync(_uow, ct);
        }
    }

    internal static class PostingProfileMapper
    {
        public static async Task<PostingProfileDto> LoadAsync(IAccountingUnitOfWork uow, CancellationToken ct)
        {
            var mappings = await uow.PostingMappings.GetAllAsync(ct);
            var accounts = await uow.Accounts.GetByIdsAsync(mappings.Select(m => m.AccountId), ct);
            return new PostingProfileDto
            {
                Mappings = mappings.Select(m =>
                {
                    var account = accounts.FirstOrDefault(a => a.Id == m.AccountId);
                    return new PostingMappingDto
                    {
                        PostingKey = m.PostingKey,
                        AccountId = m.AccountId,
                        AccountNumber = account?.AccountNumber ?? string.Empty,
                        AccountName = account?.Name ?? string.Empty
                    };
                }).ToList()
            };
        }
    }

    public class ReverseJournalEntryCommandHandler : IRequestHandler<ReverseJournalEntryCommand, JournalEntryDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalPostingService _posting;

        public ReverseJournalEntryCommandHandler(IAccountingUnitOfWork uow, IJournalPostingService posting)
        {
            _uow = uow;
            _posting = posting;
        }

        public async Task<JournalEntryDto> Handle(ReverseJournalEntryCommand request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new BusinessRuleException("Accounting", "A reason is required to reverse a journal entry.");

            var original = await _uow.JournalEntries.GetByIdAsync(request.JournalEntryId, ct)
                ?? throw new KeyNotFoundException($"Journal entry {request.JournalEntryId} not found.");
            if (original.Status != (int)JournalStatus.Posted)
                throw new BusinessRuleException("Accounting", $"Journal entry {original.EntryNumber} is not posted (or is already reversed).");

            var reversal = await _posting.ReverseEntryAsync(original, request.Reason, DateTime.UtcNow.Date, ct)
                ?? throw new BusinessRuleException("Accounting", "Automatic posting is switched off, so the entry cannot be reversed.");
            await _uow.SaveChangesAsync(ct);
            return GetJournalEntryQueryHandler.MapJournalEntryToDto(reversal);
        }
    }
}
