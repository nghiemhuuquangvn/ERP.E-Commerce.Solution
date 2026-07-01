namespace ERP.Shared.Core.Domain
{
    /// <summary>
    /// Base class for a per-language text row owned by a specific aggregate.
    /// Concrete subclasses (e.g. ProductTranslation, DepartmentTranslation) are declared
    /// in the owning service's own Domain project and persisted in that service's own
    /// database — translation rows are never shared across service boundaries.
    /// The owning aggregate keeps its own Name/Description columns as the fallback
    /// value for the default language; translation rows are an additive overlay.
    /// </summary>
    public abstract class TranslationEntity : BaseEntity
    {
        /// <summary>Id of the aggregate this translation belongs to (e.g. ProductId).</summary>
        public Guid OwnerId { get; protected set; }

        /// <summary>Culture/language code, e.g. "en-US", "vi-VN".</summary>
        public string LanguageCode { get; protected set; } = string.Empty;
    }
}
