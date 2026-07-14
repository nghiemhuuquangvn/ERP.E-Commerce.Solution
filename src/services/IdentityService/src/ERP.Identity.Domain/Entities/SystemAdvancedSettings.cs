using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>Single well-known-GUID row holding platform-wide Advanced (catch-all) setting toggles.</summary>
    public class SystemAdvancedSettings : BaseEntity
    {
        public static readonly Guid WellKnownId = Guid.Parse("E7000000-0000-0000-0000-000000000001");

        public bool DebugModeAllowed { get; private set; } = false;

        protected SystemAdvancedSettings() { }

        public static SystemAdvancedSettings CreateDefault()
        {
            return new SystemAdvancedSettings
            {
                Id = WellKnownId,
                DebugModeAllowed = false,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(bool debugModeAllowed)
        {
            DebugModeAllowed = debugModeAllowed;
        }
    }
}
