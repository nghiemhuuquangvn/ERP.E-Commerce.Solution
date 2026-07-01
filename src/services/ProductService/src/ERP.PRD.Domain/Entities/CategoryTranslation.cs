using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Entities
{
    /// <summary>
    /// Per-language overlay for a Category's translatable content (Name).
    /// The owning Category keeps its own Name as the fallback/default-language value.
    /// </summary>
    public class CategoryTranslation : TranslationEntity
    {
        public string Name { get; set; } = string.Empty;

        public static CategoryTranslation Create(Guid categoryId, string languageCode, string name)
        {
            return new CategoryTranslation
            {
                Id = Guid.NewGuid(),
                OwnerId = categoryId,
                LanguageCode = languageCode,
                Name = name,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }
}
