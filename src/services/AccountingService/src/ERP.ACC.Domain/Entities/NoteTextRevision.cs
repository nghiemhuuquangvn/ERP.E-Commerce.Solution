using ERP.Shared.Core.Domain;

namespace ERP.ACC.Domain.Entities
{
    /// <summary>
    /// One change of a note text (ROADMAP P1-2l): the text a section got for a language and fiscal year, who saved it (<see cref="BaseEntity.CreatedBy"/>) and when
    /// (<see cref="BaseEntity.CreatedAt"/>). Append-only: the current text lives in <see cref="NoteText"/>, this is its history; an empty text records a clearing.
    /// </summary>
    public class NoteTextRevision : AggregateRoot
    {
        public string SectionKey { get; private set; } = string.Empty;
        public string Language { get; private set; } = "vi";
        public int FiscalYear { get; private set; }
        public string Text { get; private set; } = string.Empty;

        private NoteTextRevision() { }

        public static NoteTextRevision Record(Guid tenantId, string sectionKey, string language, int fiscalYear, string text, string? changedBy) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SectionKey = sectionKey,
            Language = language,
            FiscalYear = fiscalYear,
            Text = (text ?? string.Empty).Trim(),
            CreatedBy = string.IsNullOrWhiteSpace(changedBy) ? null : changedBy.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
    }
}
