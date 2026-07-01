using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class UnitOfMeasureTranslation : TranslationEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;

        public static UnitOfMeasureTranslation Create(Guid ownerId, string languageCode, string name, string symbol)
        {
            return new UnitOfMeasureTranslation
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                LanguageCode = languageCode,
                Name = name,
                Symbol = symbol,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }
}
