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
        public string Text { get; private set; } = string.Empty;

        private NoteText() { }

        public static NoteText Create(Guid tenantId, string sectionKey, string text)
        {
            var note = new NoteText { Id = Guid.NewGuid(), TenantId = tenantId, SectionKey = sectionKey.Trim(), CreatedAt = DateTime.UtcNow };
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
