using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    /// <summary>One VAT line of an invoice or a bill (ROADMAP P1-3b): goods taxed at 0 / 5 / 8 / 10 % or not subject to VAT (KCT). A document without lines has one implicit line from its own net / rate / tax.</summary>
    public class VatLine : AggregateRoot
    {
        public const int InvoiceDocument = 1;
        public const int BillDocument = 2;
        public const int Taxable = 0;
        public const int NotTaxable = 1;

        public int DocumentType { get; private set; }
        public Guid DocumentId { get; private set; }
        public int Category { get; private set; }
        public decimal RatePercent { get; private set; }
        public decimal Net { get; private set; }
        public decimal Tax { get; private set; }
        public string? Text { get; private set; }

        private VatLine() { }

        public static VatLine Create(int documentType, Guid documentId, int category, decimal ratePercent, decimal net, decimal tax, string? text) => new()
        {
            Id = Guid.NewGuid(),
            DocumentType = documentType,
            DocumentId = documentId,
            Category = category,
            RatePercent = ratePercent,
            Net = net,
            Tax = tax,
            Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
    }
}
