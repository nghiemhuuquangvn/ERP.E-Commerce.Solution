using System;

namespace ERP.Identity.Application.DTOs.Settings
{
    public record SystemWorkflowSettingsDto(bool MultiLevelApprovalAvailable);
    public record UpdateSystemWorkflowSettingsRequest(bool MultiLevelApprovalAvailable);

    public record TenantWorkflowSettingsDto(Guid TenantId, bool MultiLevelApprovalEnabled, int DefaultApprovalLevels, decimal? AutoApproveThresholdAmount);
    public record UpdateTenantWorkflowSettingsRequest(bool MultiLevelApprovalEnabled, int DefaultApprovalLevels, decimal? AutoApproveThresholdAmount);
}
