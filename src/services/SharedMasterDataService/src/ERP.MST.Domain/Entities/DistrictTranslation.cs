using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class DistrictTranslation : TranslationEntity, ISharedReferenceData
    {
        public string Name { get; set; } = string.Empty;

        public static DistrictTranslation Create(Guid ownerId, string languageCode, string name)
        {
            return new DistrictTranslation
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
