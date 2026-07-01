using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Entities
{
    /// <summary>
    /// Per-language overlay for a Product's translatable content (Name, Description).
    /// The owning Product keeps its own Name/Description as the fallback/default-language value.
    /// </summary>
    public class ProductTranslation : TranslationEntity
    {
        public string Name { get; set; } = string.Empty;
        public new string? Description { get; set; }

        public static ProductTranslation Create(Guid productId, string languageCode, string name, string? description)
        {
            return new ProductTranslation
            {
                Id = Guid.NewGuid(),
                OwnerId = productId,
                LanguageCode = languageCode,
                Name = name,
                Description = description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }
}
