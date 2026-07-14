using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// A platform-wide named feature flag. Standalone POCO (not BaseEntity, like ApiKey/
    /// SecurityAuditLog) — this is a keyed collection with an admin-defined Key, not a
    /// singleton config row like every other Settings category.
    /// </summary>
    public class SystemFeatureFlag
    {
        public Guid Id { get; private set; }
        public string Key { get; private set; } = default!;
        public bool IsEnabled { get; private set; }
        public string? Description { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private SystemFeatureFlag() { }

        public static SystemFeatureFlag Create(string key, bool isEnabled, string? description)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key is required", nameof(key));

            var now = DateTime.UtcNow;
            return new SystemFeatureFlag
            {
                Id = Guid.NewGuid(),
                Key = key,
                IsEnabled = isEnabled,
                Description = description,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        public void Update(bool isEnabled, string? description)
        {
            IsEnabled = isEnabled;
            Description = description;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
