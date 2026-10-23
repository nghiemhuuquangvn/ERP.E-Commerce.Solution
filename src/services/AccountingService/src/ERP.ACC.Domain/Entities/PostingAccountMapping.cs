using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    /// <summary>
    /// Posting keys: what a generated journal line stands for. Business flows post "Dr AccountsReceivable / Cr SalesRevenue"
    /// and the tenant's <see cref="PostingAccountMapping"/> decides which account of its chart that is, so moving to another
    /// chart (the Vietnamese VAS one, ROADMAP P1-2) means editing the mapping, not the code.
    /// </summary>
    public static class PostingKeys
    {
        public const string Cash = "Cash";
        public const string Bank = "Bank";
        public const string AccountsReceivable = "AccountsReceivable";
        public const string AccountsPayable = "AccountsPayable";
        public const string SalesRevenue = "SalesRevenue";
        public const string Inventory = "Inventory";
        public const string VatOutput = "VatOutput";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Cash, Bank, AccountsReceivable, AccountsPayable, SalesRevenue, Inventory, VatOutput
        };

        /// <summary>Account numbers of the seeded 14-account chart, used when a tenant has no mapping yet.</summary>
        public static readonly IReadOnlyDictionary<string, string> DefaultAccountNumbers = new Dictionary<string, string>
        {
            [Cash] = "1000",
            [Bank] = "1000",   // the seeded chart has no separate bank account
            [AccountsReceivable] = "1100",
            [AccountsPayable] = "2000",
            [SalesRevenue] = "4000",
            [Inventory] = "1200",
            [VatOutput] = "2200",
        };
    }

    /// <summary>Which account a posting key uses for one tenant.</summary>
    public class PostingAccountMapping : AggregateRoot
    {
        public string PostingKey { get; private set; } = string.Empty;
        public Guid AccountId { get; private set; }

        private PostingAccountMapping() { }

        public static PostingAccountMapping Create(Guid tenantId, string postingKey, Guid accountId)
        {
            if (!PostingKeys.All.Contains(postingKey))
                throw new ArgumentException($"Unknown posting key '{postingKey}'.", nameof(postingKey));
            if (accountId == Guid.Empty)
                throw new ArgumentException("AccountId is required", nameof(accountId));

            return new PostingAccountMapping
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PostingKey = postingKey,
                AccountId = accountId,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void PointTo(Guid accountId)
        {
            if (accountId == Guid.Empty)
                throw new ArgumentException("AccountId is required", nameof(accountId));
            AccountId = accountId;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
