using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class SupportedLanguage : AggregateRoot, ISharedReferenceData
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NativeName { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }

        public static SupportedLanguage Create(
            string code,
            string name,
            string nativeName,
            bool isDefault = false,
            int sortOrder = 0)
        {
            var language = new SupportedLanguage
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                NativeName = nativeName,
                IsDefault = isDefault,
                IsActive = true,
                SortOrder = sortOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return language;
        }
    }
}
