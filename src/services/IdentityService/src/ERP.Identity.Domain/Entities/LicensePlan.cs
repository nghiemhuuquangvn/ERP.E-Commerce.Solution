using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// A reusable, SuperAdmin-defined license plan catalog entry (e.g. Starter/
    /// Professional/Enterprise). A tenant license issued from a plan copies the plan's
    /// module set + MaxUsers as a starting point (snapshot, not a live link) — editing a
    /// plan afterwards does not retroactively change tenants already issued against it.
    /// Standalone POCO (not BaseEntity), same shape as SystemFeatureFlag.
    /// </summary>
    public class LicensePlan
    {
        public Guid Id { get; private set; }
        public string PlanCode { get; private set; } = default!;
        public string PlanName { get; private set; } = default!;
        public string? Description { get; private set; }
        public int? MaxUsers { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private LicensePlan() { }

        public static LicensePlan Create(string planCode, string planName, string? description, int? maxUsers, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(planCode))
                throw new ArgumentException("Plan code is required", nameof(planCode));
            if (string.IsNullOrWhiteSpace(planName))
                throw new ArgumentException("Plan name is required", nameof(planName));

            var now = DateTime.UtcNow;
            return new LicensePlan
            {
                Id = Guid.NewGuid(),
                PlanCode = planCode.ToUpperInvariant(),
                PlanName = planName,
                Description = description,
                MaxUsers = maxUsers,
                IsActive = isActive,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        public void Update(string planName, string? description, int? maxUsers, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(planName))
                throw new ArgumentException("Plan name is required", nameof(planName));

            PlanName = planName;
            Description = description;
            MaxUsers = maxUsers;
            IsActive = isActive;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
