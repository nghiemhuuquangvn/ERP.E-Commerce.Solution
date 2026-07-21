# Notification Privacy Model

**Service:** NotificationService (Port 5013, Code: NTF)

## Context

A platform-wide authentication/authorization audit (2026-07-21) found
`PreferencesController` (all actions) and `NotificationsController.GetById`/
`GetHistory` had no authentication check at all — reachable by anyone,
returning another user's private notification history/preferences on
request.

## Rule established

A caller can only read their own notification history/preferences. Reading
another recipient's data requires the caller to hold the elevated
`notifications:send` permission (Admin only in the current role set) — there
is no separate "view any recipient's notifications" permission; the write
permission doubles as the cross-recipient override, since only Admin holds it
and Admin is expected to have full visibility for support purposes.

## How it's enforced

A new `notifications:view` permission (Admin/Manager/Viewer) gates the base
"can this caller read notification data at all" check, matching the pattern
established for every other domain's `:view`/`:manage` pair. On top of that,
each action does an identity comparison specific to how it's keyed:

- `PreferencesController` — keyed by `userId` (route param): must equal the
  caller's own `UserId`, or the caller holds `notifications:send`.
- `NotificationsController.GetHistory` — keyed by `recipientId` (route
  param): same comparison.
- `NotificationsController.GetById` — keyed only by `NotificationId` (no
  identity in the URL at all); the fix fetches the record first (after the
  base permission check), then compares its `RecipientId` field against the
  caller's identity before returning it.

This means `notifications:view` alone is **not** sufficient for
Manager/Viewer to read arbitrary recipients' data — the permission only
grants "you may use this feature," the identity comparison decides "whose
data you may see through it." Don't remove the ownership check under the
assumption the permission check alone is enough; that reintroduces the exact
IDOR this fix closed.
