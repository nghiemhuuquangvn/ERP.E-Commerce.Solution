using ERP.NTF.Domain.Enums;

namespace ERP.NTF.Application.DTOs;

public class NotificationDto
{
    public Guid Id { get; set; }
    public string RecipientId { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? ScheduledFor { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public List<NotificationLogDto> Logs { get; set; } = new();
}

public class NotificationLogDto
{
    public string EventType { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime EventTimestamp { get; set; }
}

public class NotificationTemplateDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Variables { get; set; }
    public bool IsActive { get; set; }
    /// <summary>True for the platform's default; false when this tenant wrote its own template with the same code.</summary>
    public bool IsPlatformDefault { get; set; }
}

public class TemplatePreviewResult
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<string> UnknownPlaceholders { get; set; } = new();
}

public class NotificationPreferenceDto
{
    public Guid UserId { get; set; }
    public bool EmailEnabled { get; set; }
    public bool SmsEnabled { get; set; }
    public bool PushEnabled { get; set; }
    public bool InAppEnabled { get; set; }
}

public class BulkSendResult
{
    public int Queued { get; set; }
    public int Skipped { get; set; }
    public List<Guid> NotificationIds { get; set; } = new();
}

public class WebhookDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    /// <summary>Whether a signing secret is stored. The secret itself is never returned.</summary>
    public bool HasSecret { get; set; }
    public List<string> EventTypes { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    /// <summary>The most recent delivery attempt, so the list can show whether the endpoint works.</summary>
    public WebhookDeliveryDto? LastDelivery { get; set; }
}

public class WebhookDeliveryDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int? StatusCode { get; set; }
    public string? Error { get; set; }
    public int DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WebhookDeliveryPage
{
    public List<WebhookDeliveryDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public class WebhookEventTypeDto
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class WebhookTestResult
{
    public bool Success { get; set; }
    public int? StatusCode { get; set; }
    public int DurationMs { get; set; }
    public string? Error { get; set; }
}
