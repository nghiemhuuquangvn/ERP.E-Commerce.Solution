using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    public class BankStatement : AggregateRoot
    {
        public Guid BankAccountId { get; private set; }
        public DateTime StatementDate { get; private set; }
        public decimal OpeningBalance { get; private set; }
        public decimal ClosingBalance { get; private set; }
        public int Status { get; private set; }
        public DateTime? ReconciledAt { get; private set; }
        public string? Notes { get; private set; }

        private BankStatement() { }

        public static BankStatement Create(
            Guid bankAccountId,
            DateTime statementDate,
            decimal openingBalance,
            decimal closingBalance,
            string? notes = null)
        {
            return new BankStatement
            {
                Id = Guid.NewGuid(),
                BankAccountId = bankAccountId,
                StatementDate = statementDate,
                OpeningBalance = openingBalance,
                ClosingBalance = closingBalance,
                Status = 0,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Reconcile(string? notes = null)
        {
            if (Status == 1)
                throw new InvalidOperationException("Bank statement is already reconciled.");

            Status = 1;
            ReconciledAt = DateTime.UtcNow;
            if (notes is not null) Notes = notes;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
