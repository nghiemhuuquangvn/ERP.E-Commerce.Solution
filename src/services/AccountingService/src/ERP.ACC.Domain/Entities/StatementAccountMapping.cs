using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    /// <summary>
    /// A tenant's own placement of accounts on a statement row (ROADMAP P1-2j): every account whose number starts with <see cref="AccountPrefix"/> counts in the row
    /// <see cref="RowCode"/> of the statement <see cref="StatementId"/> (B01-DN / B02-DN), in addition to what the standard layout of the chart already places there.
    /// A tenant on a chart of its own uses it to take its accounts out of the "unmapped" list. <see cref="Side"/>: 0 net balance, 1 debit balances only, 2 credit balances only.
    /// </summary>
    public class StatementAccountMapping : AggregateRoot
    {
        public string StatementId { get; private set; } = string.Empty;
        public string RowCode { get; private set; } = string.Empty;
        public string AccountPrefix { get; private set; } = string.Empty;
        public int Side { get; private set; }

        private StatementAccountMapping() { }

        public static StatementAccountMapping Create(string statementId, string rowCode, string accountPrefix, int side)
        {
            if (string.IsNullOrWhiteSpace(statementId))
                throw new ArgumentException("A statement is required.", nameof(statementId));
            if (string.IsNullOrWhiteSpace(rowCode))
                throw new ArgumentException("A row code is required.", nameof(rowCode));
            if (string.IsNullOrWhiteSpace(accountPrefix))
                throw new ArgumentException("An account number prefix is required.", nameof(accountPrefix));
            if (side < 0 || side > 2)
                throw new ArgumentException("Unknown side.", nameof(side));

            return new StatementAccountMapping
            {
                Id = Guid.NewGuid(),
                StatementId = statementId.Trim(),
                RowCode = rowCode.Trim(),
                AccountPrefix = accountPrefix.Trim(),
                Side = side,
                CreatedAt = DateTime.UtcNow,
            };
        }

        public void SetSide(int side)
        {
            if (side < 0 || side > 2)
                throw new ArgumentException("Unknown side.", nameof(side));
            Side = side;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
