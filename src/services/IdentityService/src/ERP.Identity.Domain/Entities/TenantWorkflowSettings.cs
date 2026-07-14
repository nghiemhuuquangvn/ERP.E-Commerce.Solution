using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>Per-tenant Workflow configuration. One row per AuthTenant (TenantId is unique).</summary>
    public class TenantWorkflowSettings : BaseEntity
    {
        public Guid TenantId { get; private set; }
        public bool MultiLevelApprovalEnabled { get; private set; } = false;
        public int DefaultApprovalLevels { get; private set; } = 1;
        public decimal? AutoApproveThresholdAmount { get; private set; }

        protected TenantWorkflowSettings() { }

        public static TenantWorkflowSettings CreateDefault(Guid tenantId)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required", nameof(tenantId));

            return new TenantWorkflowSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MultiLevelApprovalEnabled = false,
                DefaultApprovalLevels = 1,
                AutoApproveThresholdAmount = null,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(bool multiLevelApprovalEnabled, int defaultApprovalLevels, decimal? autoApproveThresholdAmount)
        {
            MultiLevelApprovalEnabled = multiLevelApprovalEnabled;
            DefaultApprovalLevels = defaultApprovalLevels;
            AutoApproveThresholdAmount = autoApproveThresholdAmount;
        }
    }
}
