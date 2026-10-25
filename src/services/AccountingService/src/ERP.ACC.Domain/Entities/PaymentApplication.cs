using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    /// <summary>
    /// One payment (or refund) applied to an invoice or a bill, with the day it happened (ROADMAP P1-2j). The document keeps only a running total, so without these
    /// rows the amount still open on an earlier date is unknown; with them the maturity split of B01 / B09 is exact for any past date. A refund has a negative amount.
    /// Rows made from the documents' state when this register was introduced are <see cref="IsEstimated"/> (their date is the best known, not recorded).
    /// </summary>
    public class PaymentApplication : AggregateRoot
    {
        public const int InvoiceDocument = 1;
        public const int BillDocument = 2;

        public int DocumentType { get; private set; }
        public Guid DocumentId { get; private set; }
        public DateTime PaidOn { get; private set; }
        public decimal Amount { get; private set; }

        /// <summary>The business event that made it (payment id, refund id, "manual:&lt;document&gt;"), unique per tenant so a redelivered event adds nothing twice.</summary>
        public string? SourceKey { get; private set; }
        public bool IsEstimated { get; private set; }

        private PaymentApplication() { }

        public static PaymentApplication Create(int documentType, Guid documentId, DateTime paidOn, decimal amount, string? sourceKey, bool isEstimated = false)
        {
            if (documentType != InvoiceDocument && documentType != BillDocument)
                throw new ArgumentException("Unknown document type.", nameof(documentType));
            if (documentId == Guid.Empty)
                throw new ArgumentException("DocumentId is required.", nameof(documentId));
            if (amount == 0m)
                throw new ArgumentException("A payment application moves money.", nameof(amount));

            return new PaymentApplication
            {
                Id = Guid.NewGuid(),
                DocumentType = documentType,
                DocumentId = documentId,
                PaidOn = paidOn.Date,
                Amount = Math.Round(amount, 2),
                SourceKey = sourceKey,
                IsEstimated = isEstimated,
                CreatedAt = DateTime.UtcNow,
            };
        }
    }
}
