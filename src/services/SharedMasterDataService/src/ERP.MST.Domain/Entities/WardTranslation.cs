using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class WardTranslation : TranslationEntity, ISharedReferenceData
    {
        public string Name { get; set; } = string.Empty;

        public static WardTranslation Create(Guid ownerId, string languageCode, string name)
        {
            return new WardTranslation
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
