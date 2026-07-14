using System;

namespace ERP.Identity.Application.DTOs.Settings
{
    public record SystemSchedulerSettingsDto(bool BackgroundJobsEnabled);
    public record UpdateSystemSchedulerSettingsRequest(bool BackgroundJobsEnabled);

    public record TenantSchedulerSettingsDto(Guid TenantId, int OutboxPollingIntervalSeconds, int MaxRetryAttempts);
    public record UpdateTenantSchedulerSettingsRequest(int OutboxPollingIntervalSeconds, int MaxRetryAttempts);
}
