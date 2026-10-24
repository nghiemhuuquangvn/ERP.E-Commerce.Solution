using ERP.ACC.Application.Interfaces;
using ERP.ACC.Application.Posting;
using ERP.ACC.Domain.Entities;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Infrastructure.Exceptions;
using MediatR;

namespace ERP.ACC.Application.Handlers
{
    public record ChartTemplateDto(string Id, string Name, string Description, int AccountCount);

    public record ApplyChartTemplateResultDto(string TemplateId, int AccountsCreated, int PostingKeysMapped);

    public class ListChartTemplatesQuery : IRequest<List<ChartTemplateDto>> { }

    /// <summary>Applies a ready-made chart of accounts (ROADMAP P1-2). Allowed only while the tenant has no journal entries.</summary>
    public class ApplyChartTemplateCommand : IRequest<ApplyChartTemplateResultDto>
    {
        public string TemplateId { get; set; } = string.Empty;
    }

    public class ListChartTemplatesQueryHandler : IRequestHandler<ListChartTemplatesQuery, List<ChartTemplateDto>>
    {
        public Task<List<ChartTemplateDto>> Handle(ListChartTemplatesQuery request, CancellationToken cancellationToken) =>
            Task.FromResult(ChartTemplates.All.Select(t => new ChartTemplateDto(t.Id, t.Name, t.Description, t.Accounts.Count)).ToList());
    }

    public class ApplyChartTemplateCommandHandler : IRequestHandler<ApplyChartTemplateCommand, ApplyChartTemplateResultDto>
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly ITenantProvider _tenant;

        public ApplyChartTemplateCommandHandler(IAccountingUnitOfWork uow, ITenantProvider tenant)
        {
            _uow = uow;
            _tenant = tenant;
        }

        public async Task<ApplyChartTemplateResultDto> Handle(ApplyChartTemplateCommand request, CancellationToken cancellationToken)
        {
            var tenantId = _tenant.CurrentTenantId ?? throw new BusinessRuleException("Accounting", "A tenant is required.");
            var template = ChartTemplates.Find(request.TemplateId)
                ?? throw new BusinessRuleException("Accounting", $"Unknown chart template '{request.TemplateId}'. Available: {string.Join(", ", ChartTemplates.All.Select(t => t.Id))}.");

            if (await _uow.JournalEntries.AnyAsync(cancellationToken))
                throw new BusinessRuleException("Accounting",
                    "This tenant already has journal entries, so its chart cannot be replaced. Add accounts and re-map the posting profile instead.");

            var existing = await _uow.Accounts.GetAllTrackedAsync(cancellationToken);
            var byNumber = existing.ToDictionary(a => a.AccountNumber);
            var created = 0;

            try
            {
                foreach (var entry in template.Accounts)
                {
                    if (byNumber.ContainsKey(entry.Number))
                        continue;

                    var parent = entry.ParentNumber != null ? byNumber[entry.ParentNumber] : null;
                    var account = Account.Create(entry.Number, entry.Name, (int)entry.Type, (int)entry.Normal, parent);
                    account.IsControl = entry.IsControl;
                    await _uow.Accounts.AddAsync(account, cancellationToken);
                    byNumber[entry.Number] = account;
                    created++;
                }
            }
            catch (InvalidOperationException ex)
            {
                throw new BusinessRuleException("Accounting", $"The tenant's existing accounts conflict with the template: {ex.Message}");
            }

            var current = await _uow.PostingMappings.GetAllAsync(cancellationToken);
            var mapped = 0;
            foreach (var key in PostingKeys.All)
            {
                if (!template.PostingMap.TryGetValue(key, out var number) || !byNumber.TryGetValue(number, out var account))
                    continue;

                var mapping = current.FirstOrDefault(m => m.PostingKey == key);
                if (mapping != null)
                    mapping.PointTo(account.Id);
                else
                    await _uow.PostingMappings.AddAsync(PostingAccountMapping.Create(tenantId, key, account.Id), cancellationToken);
                mapped++;
            }

            await _uow.SaveChangesAsync(cancellationToken);
            return new ApplyChartTemplateResultDto(template.Id, created, mapped);
        }
    }
}
