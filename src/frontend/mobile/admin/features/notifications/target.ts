import type { AppNotification } from './api'

/** Where a notification leads: the detail screen of its record, when the type is known and the user may open that record. */
const TARGETS: Record<string, { path: (id: string) => string; permission: string }> = {
  PurchaseOrder: { path: (id) => `/purchase/${id}`, permission: 'purchase_order:read' },
  SalesOrder: { path: (id) => `/sales/${id}`, permission: 'sales_order:read' },
  LeaveRequest: { path: (id) => `/hrm/leave/${id}`, permission: 'hrm:leave:manage' },
}

type Link = Pick<AppNotification, 'entityType' | 'entityId'>

/** The route to open, or null when there is no link, the type is unknown, or the user lacks the permission (the row then just expands). */
export function notificationTarget(link: Link, hasPermission: (permission: string) => boolean): string | null {
  if (!link.entityType || !link.entityId) return null
  const target = TARGETS[link.entityType]
  if (!target || !hasPermission(target.permission)) return null
  return target.path(link.entityId)
}
