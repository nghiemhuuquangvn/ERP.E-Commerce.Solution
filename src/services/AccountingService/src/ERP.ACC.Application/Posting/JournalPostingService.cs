using System.Globalization;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Enums;
using ERP.Shared.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Application.Posting
{
    public sealed class JournalPostingService : IJournalPostingService
    {
        private readonly IAccountingUnitOfWork _uow;
        private readonly IJournalNumberGenerator _numbers;
        private readonly ITenantProvider _tenant;
        private readonly IConfiguration _configuration;
        private readonly ILogger<JournalPostingService> _logger;

        // Staged but not yet saved in this scope: a second posting for the same month/source must see them.
        private readonly Dictionary<string, FiscalPeriod> _stagedPeriods = new();
        private readonly Dictionary<string, JournalEntry> _stagedEntries = new();
        private Dictionary<string, Account>? _profile;

        public JournalPostingService(
            IAccountingUnitOfWork uow,
            IJournalNumberGenerator numbers,
            ITenantProvider tenant,
            IConfiguration configuration,
            ILogger<JournalPostingService> logger)
        {
            _uow = uow;
            _numbers = numbers;
            _tenant = tenant;
            _configuration = configuration;
            _logger = logger;
        }

        private bool Enabled => _configuration.GetValue("Accounting:AutoPosting:Enabled", true);

        public async Task<JournalEntry?> PostAsync(PostingRequest request, CancellationToken ct = default)
        {
            if (!Enabled)
                return null;

            var tenantId = _tenant.CurrentTenantId
                ?? throw new InvalidOperationException("Posting needs a tenant (TenantScope or an authenticated request).");
            var sourceKey = JournalEntry.BuildSourceKey(request.SourceType, request.SourceId);

            // Idempotent: a redelivered event (or a second call) finds the entry that already exists.
            if (_stagedEntries.TryGetValue(sourceKey, out var staged))
                return staged;
            var existing = await _uow.JournalEntries.GetBySourceKeyAsync(sourceKey, ct);
            if (existing != null)
                return existing;

            var profile = await LoadPostingProfileAsync(tenantId, ct);
            var drafts = new List<JournalLineDraft>();
            foreach (var line in request.Lines.Where(l => l.Debit > 0 || l.Credit > 0))
            {
                var accountId = line.AccountId ?? Resolve(profile, line.PostingKey).Id;
                drafts.Add(new JournalLineDraft(accountId, line.Debit, line.Credit, line.Description));
            }

            var (period, entryDate) = await ResolvePeriodAsync(request.Date, ct);
            var number = await _numbers.NextAsync(tenantId, entryDate.Year, ct);

            var entry = JournalEntry.CreatePosted(number, period.Id, entryDate, request.Description,
                request.SourceType, request.SourceId, drafts);
            await _uow.JournalEntries.AddAsync(entry, ct);
            _stagedEntries[sourceKey] = entry;
            return entry;
        }

        public async Task<JournalEntry?> ReverseAsync(string sourceType, Guid sourceId, string reason, DateTime date, CancellationToken ct = default)
        {
            if (!Enabled)
                return null;

            var original = _stagedEntries.GetValueOrDefault(JournalEntry.BuildSourceKey(sourceType, sourceId))
                ?? await _uow.JournalEntries.GetBySourceKeyAsync(JournalEntry.BuildSourceKey(sourceType, sourceId), ct);
            if (original == null)
            {
                _logger.LogWarning("Nothing to reverse: no journal entry for {SourceType} {SourceId}", sourceType, sourceId);
                return null;
            }

            return await ReverseEntryAsync(original, reason, date, ct);
        }

        public async Task<JournalEntry?> ReverseEntryAsync(JournalEntry original, string reason, DateTime date, CancellationToken ct = default)
        {
            if (!Enabled)
                return null;

            var tenantId = _tenant.CurrentTenantId
                ?? throw new InvalidOperationException("Posting needs a tenant (TenantScope or an authenticated request).");

            var reversalKey = JournalEntry.BuildSourceKey((original.ReferenceType ?? "Manual") + "Reversal", original.ReferenceId ?? original.Id);
            if (_stagedEntries.TryGetValue(reversalKey, out var stagedReversal))
                return stagedReversal;
            var already = await _uow.JournalEntries.GetBySourceKeyAsync(reversalKey, ct);
            if (already != null)
                return already;
            if (original.Status != (int)JournalStatus.Posted)
                return null;

            var (period, entryDate) = await ResolvePeriodAsync(date, ct);
            var number = await _numbers.NextAsync(tenantId, entryDate.Year, ct);
            var reversal = original.Reverse(number, period.Id, entryDate, reason);

            await _uow.JournalEntries.AddAsync(reversal, ct);
            await _uow.JournalEntries.UpdateAsync(original, ct);
            _stagedEntries[reversalKey] = reversal;
            return reversal;
        }

        // ---- posting profile -------------------------------------------------------------------------------------

        private async Task<Dictionary<string, Account>> LoadPostingProfileAsync(Guid tenantId, CancellationToken ct)
        {
            if (_profile != null)
                return _profile;

            var mappings = await _uow.PostingMappings.GetAllAsync(ct);
            var missing = PostingKeys.All.Where(k => mappings.All(m => m.PostingKey != k)).ToList();
            if (missing.Count > 0)
                mappings.AddRange(await ProvisionDefaultsAsync(tenantId, missing, ct));

            var accounts = (await _uow.Accounts.GetByIdsAsync(mappings.Select(m => m.AccountId), ct))
                .ToDictionary(a => a.Id);
            // Accounts created a moment ago by ProvisionDefaultsAsync are not in the database yet.
            foreach (var staged in _provisionedAccounts.Values)
                accounts.TryAdd(staged.Id, staged);

            _parentIds = await _uow.Accounts.GetParentAccountIdsAsync(ct) ?? new HashSet<Guid>();

            var profile = new Dictionary<string, Account>();
            foreach (var mapping in mappings)
            {
                if (!accounts.TryGetValue(mapping.AccountId, out var account))
                    throw new PostingConfigurationException(mapping.PostingKey,
                        $"Posting key '{mapping.PostingKey}' points to an account that does not exist.");
                profile[mapping.PostingKey] = account;
            }

            return _profile = profile;
        }

        private readonly Dictionary<string, Account> _provisionedAccounts = new();
        private HashSet<Guid>? _parentIds;

        /// <summary>
        /// A key with no mapping gets its default account (by number in the shipped chart). A tenant with no accounts at all
        /// receives the whole default chart first; one that already has its own chart is never given extra accounts, a
        /// missing number is a configuration problem instead.
        /// </summary>
        private async Task<List<PostingAccountMapping>> ProvisionDefaultsAsync(Guid tenantId, List<string> keys, CancellationToken ct)
        {
            var existingAccounts = await _uow.Accounts.GetAllAsync(ct);
            var byNumber = existingAccounts.ToDictionary(a => a.AccountNumber);

            var template = ChartTemplates.Find(_configuration["Accounting:DefaultChartTemplate"]) ?? ChartTemplates.Find(ChartTemplates.Simple14)!;
            if (existingAccounts.Count == 0)
            {
                foreach (var account in template.BuildAccounts())
                {
                    await _uow.Accounts.AddAsync(account, ct);
                    byNumber[account.AccountNumber] = account;
                    _provisionedAccounts[account.AccountNumber] = account;
                }
                _logger.LogInformation("Tenant {TenantId}: created the {Template} chart of accounts", tenantId, template.Id);
            }

            var created = new List<PostingAccountMapping>();
            foreach (var key in keys)
            {
                // The configured template's account first, then the other templates' (a tenant that already has its own chart may match one of them).
                var number = new[] { template }.Concat(ChartTemplates.All.Where(t => t.Id != template.Id))
                    .Select(t => t.PostingMap.GetValueOrDefault(key))
                    .FirstOrDefault(n => n != null && byNumber.ContainsKey(n))
                    ?? template.PostingMap.GetValueOrDefault(key) ?? PostingKeys.DefaultAccountNumbers[key];
                if (!byNumber.TryGetValue(number, out var account))
                {
                    // Not fatal here: the flows that do not use this key must keep posting. A flow that needs it fails in Resolve.
                    _logger.LogWarning("Tenant {TenantId}: posting key '{Key}' has no account; map it (default would be {Number})",
                        tenantId, key, number);
                    continue;
                }

                var mapping = PostingAccountMapping.Create(tenantId, key, account.Id);
                await _uow.PostingMappings.AddAsync(mapping, ct);
                created.Add(mapping);
            }

            _logger.LogInformation("Tenant {TenantId}: default posting profile for {Keys}", tenantId, string.Join(", ", keys));
            return created;
        }

        private Account Resolve(Dictionary<string, Account> profile, string? postingKey)
        {
            if (string.IsNullOrEmpty(postingKey) || !profile.TryGetValue(postingKey, out var account))
                throw new PostingConfigurationException(postingKey ?? "(none)", $"Posting key '{postingKey}' has no account.");
            if (!account.IsActive)
                throw new PostingConfigurationException(postingKey,
                    $"Posting key '{postingKey}' points to account {account.AccountNumber}, which is inactive.");
            if (_parentIds != null && _parentIds.Contains(account.Id))
                throw new PostingConfigurationException(postingKey,
                    $"Posting key '{postingKey}' points to account {account.AccountNumber}, which groups sub-accounts; map it to a sub-account.");
            return account;
        }

        // ---- fiscal period ---------------------------------------------------------------------------------------

        /// <summary>
        /// The period containing the document date, created as a calendar month when there is none yet. A document dated in
        /// a closed period is booked today in the current open period (the books of a closed period do not change); the
        /// entry date follows so the reports stay consistent.
        /// </summary>
        private async Task<(FiscalPeriod Period, DateTime EntryDate)> ResolvePeriodAsync(DateTime date, CancellationToken ct)
        {
            var day = date.Date;
            var period = await GetOrCreateMonthAsync(day, ct);
            if (period.Status == (int)FiscalPeriodStatus.Open)
                return (period, day);

            var today = DateTime.UtcNow.Date;
            _logger.LogWarning("Fiscal period {Period} is closed; the entry dated {Date:yyyy-MM-dd} is booked on {Today:yyyy-MM-dd}",
                period.PeriodName, day, today);
            var current = await GetOrCreateMonthAsync(today, ct);
            if (current.Status != (int)FiscalPeriodStatus.Open)
                throw new PostingConfigurationException("FiscalPeriod", $"No open fiscal period for {today:yyyy-MM-dd}: open one before posting.");
            return (current, today);
        }

        private async Task<FiscalPeriod> GetOrCreateMonthAsync(DateTime day, CancellationToken ct)
        {
            var monthKey = day.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            if (_stagedPeriods.TryGetValue(monthKey, out var staged) && staged.StartDate <= day && day <= staged.EndDate)
                return staged;

            var existing = await _uow.FiscalPeriods.GetContainingAsync(day, ct);
            if (existing != null)
                return existing;

            var start = new DateTime(day.Year, day.Month, 1);
            var period = FiscalPeriod.Create(start.ToString("MMMM yyyy", CultureInfo.InvariantCulture), start, start.AddMonths(1).AddDays(-1));
            await _uow.FiscalPeriods.AddAsync(period, ct);
            _stagedPeriods[monthKey] = period;
            _logger.LogInformation("Opened fiscal period {Period}", period.PeriodName);
            return period;
        }
    }
}
