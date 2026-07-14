using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>Single well-known-GUID row holding platform-wide File Storage setting toggles.</summary>
    public class SystemFileStorageSettings : BaseEntity
    {
        public static readonly Guid WellKnownId = Guid.Parse("E2000000-0000-0000-0000-000000000001");

        public bool CustomStorageProviderAllowed { get; private set; } = true;
        public int DefaultMaxUploadSizeMb { get; private set; } = 10;

        protected SystemFileStorageSettings() { }

        public static SystemFileStorageSettings CreateDefault()
        {
            return new SystemFileStorageSettings
            {
                Id = WellKnownId,
                CustomStorageProviderAllowed = true,
                DefaultMaxUploadSizeMb = 10,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(bool customStorageProviderAllowed, int defaultMaxUploadSizeMb)
        {
            CustomStorageProviderAllowed = customStorageProviderAllowed;
            DefaultMaxUploadSizeMb = defaultMaxUploadSizeMb;
        }
    }
}
