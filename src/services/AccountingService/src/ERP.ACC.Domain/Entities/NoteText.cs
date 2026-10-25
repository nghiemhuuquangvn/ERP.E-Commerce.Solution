using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    /// <summary>
    /// One text of the notes to the financial statements (B09-DN, ROADMAP P1-2i) that only the accountant can write — the accounting policies, the standards
    /// applied, non-cash transactions, events after the period. Identified by a section key; one row per tenant and key. The figures of the notes are
    /// computed from the books and are not stored.
    /// </summary>
    public class NoteText : AggregateRoot
    {
        public const int MaxKeyLength = 50;
        public const int MaxTextLength = 4000;

        public string SectionKey { get; private set; } = string.Empty;

        /// <summary>"vi" or "en": the notes are written once per language (ROADMAP P1-2j).</summary>
        public string Language { get; private set; } = "vi";

        /// <summary>The fiscal year the text is written for (the year in which it ends); 0 = every year (the texts written before versions existed). A report reads the text of its year, else the latest earlier one, else year 0 (ROADMAP P1-2k).</summary>
        public int FiscalYear { get; private set; }
        public string Text { get; private set; } = string.Empty;

        private NoteText() { }

        public static NoteText Create(Guid tenantId, string sectionKey, string text, string language = "vi", int fiscalYear = 0)
        {
            if (fiscalYear < 0 || fiscalYear > 9999)
                throw new ArgumentException("The fiscal year must be 0 (every year) or a year.", nameof(fiscalYear));
            var note = new NoteText { Id = Guid.NewGuid(), TenantId = tenantId, SectionKey = sectionKey.Trim(), Language = language, FiscalYear = fiscalYear, CreatedAt = DateTime.UtcNow };
            note.Set(text);
            return note;
        }

        public void Set(string text)
        {
            var clean = (text ?? string.Empty).Trim();
            if (clean.Length > MaxTextLength)
                throw new ArgumentException($"A note text is limited to {MaxTextLength} characters.");
            Text = clean;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
