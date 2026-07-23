using ERP.Shared.Core.Domain;

namespace ERP.PRD.Domain.Entities
{
    /// <summary>
    /// Product brand/manufacturer, flat reference list (no hierarchy, unlike Category).
    /// </summary>
    public class Brand : AggregateRoot
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public bool IsActive { get; set; } = true;

        public static Brand Create(string code, string name, string? logoUrl = null)
        {
            return new Brand
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                LogoUrl = logoUrl,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(string name, string? logoUrl)
        {
            Name = name;
            LogoUrl = logoUrl;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Deactivate()
        {
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Activate()
        {
            IsActive = true;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
