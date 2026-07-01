using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class CountryTranslation : TranslationEntity
    {
        public string Name { get; set; } = string.Empty;

        public static CountryTranslation Create(Guid ownerId, string languageCode, string name)
        {
            return new CountryTranslation
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                LanguageCode = languageCode,
                Name = name,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }
}
