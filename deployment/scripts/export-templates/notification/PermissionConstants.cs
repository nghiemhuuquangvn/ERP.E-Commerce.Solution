namespace ERP.Shared.Authorization;

/// <summary>
/// Permission codes this service's own controllers reference. Trimmed for the
/// standalone export from the source monorepo's full catalog (~230 codes spanning
/// 13 other, unrelated services) down to just this service's own surface, plus a
/// Sample_* pair demonstrating the pattern.
/// </summary>
public static class PermissionConstants
{
    // Notification Permissions
    public const string Notifications_Send = "notifications:send";
    public const string Notifications_TemplatesManage = "notifications:templates:manage";
    public const string Notifications_WebhooksManage = "notifications:webhooks:manage";
    // Covers "view a notification/preference" — none of the three constants above (all
    // write-oriented) fit read-only actions like GetById/GetHistory/Preferences.Get.
    // NOTE: granting this alone is not sufficient for IDOR-prone endpoints scoped to a
    // specific recipient/user — callers must also pass an ownership check (see
    // NotificationsController/PreferencesController) unless they hold an elevated,
    // admin-scope permission (Notifications_Send) allowing cross-recipient access.
    public const string Notifications_View = "notifications:view";

    // Sample permissions demonstrating the pattern — replace with your own application's
    // permission codes, following the same "module:resource:action" convention.
    public const string Sample_WidgetsView = "sample:widgets:view";
    public const string Sample_WidgetsManage = "sample:widgets:manage";
}
