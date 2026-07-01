using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class PositionTranslation : TranslationEntity
    {
        public string Title { get; set; } = string.Empty;
        public new string? Description { get; set; }

        public static PositionTranslation Create(Guid ownerId, string languageCode, string title, string? description = null)
        {
            return new PositionTranslation
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                LanguageCode = languageCode,
                Title = title,
                Description = description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }
}
