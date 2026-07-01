using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class ProvinceTranslation : TranslationEntity
    {
        public string Name { get; set; } = string.Empty;

        public static ProvinceTranslation Create(Guid ownerId, string languageCode, string name)
        {
            return new ProvinceTranslation
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
