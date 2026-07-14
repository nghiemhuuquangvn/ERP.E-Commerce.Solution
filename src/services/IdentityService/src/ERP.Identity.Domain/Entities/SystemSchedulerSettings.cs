using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// Single well-known-GUID row holding the platform-wide Scheduler kill switch.
    /// Storage+CRUD only — does not gate the real OutboxProcessor/PromotionSchedulerJob
    /// hosted services yet.
    /// </summary>
    public class SystemSchedulerSettings : BaseEntity
    {
        public static readonly Guid WellKnownId = Guid.Parse("E3000000-0000-0000-0000-000000000001");

        public bool BackgroundJobsEnabled { get; private set; } = true;

        protected SystemSchedulerSettings() { }

        public static SystemSchedulerSettings CreateDefault()
        {
            return new SystemSchedulerSettings
            {
                Id = WellKnownId,
                BackgroundJobsEnabled = true,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(bool backgroundJobsEnabled)
        {
            BackgroundJobsEnabled = backgroundJobsEnabled;
        }
    }
}
