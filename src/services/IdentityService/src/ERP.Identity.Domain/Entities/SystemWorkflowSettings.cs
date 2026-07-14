using ERP.Identity.Domain.Common;
using System;

namespace ERP.Identity.Domain.Entities
{
    /// <summary>
    /// Single well-known-GUID row holding platform-wide Workflow setting toggles.
    /// Mirrors SystemGeneralSettings's System/Tenant two-tier pattern. Unrelated to
    /// SaleOrderService's OrderFulfillmentSaga (an order-fulfillment choreography
    /// saga) — this is generic approval-chain configuration.
    /// </summary>
    public class SystemWorkflowSettings : BaseEntity
    {
        public static readonly Guid WellKnownId = Guid.Parse("E1000000-0000-0000-0000-000000000001");

        public bool MultiLevelApprovalAvailable { get; private set; } = true;

        protected SystemWorkflowSettings() { }

        public static SystemWorkflowSettings CreateDefault()
        {
            return new SystemWorkflowSettings
            {
                Id = WellKnownId,
                MultiLevelApprovalAvailable = true,
                CreatedBy = "System",
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Update(bool multiLevelApprovalAvailable)
        {
            MultiLevelApprovalAvailable = multiLevelApprovalAvailable;
        }
    }
}
