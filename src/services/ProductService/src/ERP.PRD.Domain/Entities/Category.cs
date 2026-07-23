using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Entities
{
    /// <summary>
    /// Product category for organizing products hierarchically.
    /// Supports parent-child relationships for nested categories.
    /// </summary>
    public class Category : AggregateRoot
    {
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public Guid? ParentCategoryId { get; set; }
        public int SortOrder { get; set; }

        private readonly List<CategoryTranslation> _translations = new();
        public IReadOnlyList<CategoryTranslation> Translations => _translations.AsReadOnly();

        public static Category Create(string name, Guid? parentCategoryId = null, string? code = null)
        {
            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = name,
                Code = code,
                ParentCategoryId = parentCategoryId,
                SortOrder = 0,
                CreatedAt = DateTime.UtcNow
            };

            return category;
        }

        public void SetCode(string? code)
        {
            Code = code;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Adds or updates the translation for the given language code.
        /// Translations are an additive overlay; Name remains the default-language fallback.
        /// </summary>
        public void SetTranslation(string languageCode, string name)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                throw new ArgumentException("Language code is required");

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be empty");

            var existing = _translations.FirstOrDefault(t => t.LanguageCode == languageCode);
            if (existing != null)
            {
                existing.Name = name;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _translations.Add(CategoryTranslation.Create(Id, languageCode, name));
            }
        }
    }
}
