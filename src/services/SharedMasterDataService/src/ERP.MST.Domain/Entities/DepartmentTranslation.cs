using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class DepartmentTranslation : TranslationEntity
    {
        public string Name { get; set; } = string.Empty;

        public static DepartmentTranslation Create(Guid ownerId, string languageCode, string name)
        {
            return new DepartmentTranslation
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
