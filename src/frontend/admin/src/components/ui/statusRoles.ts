import type { BadgeVariant } from './Badge'

/**
 * The one map from a status name to a colour role (docs/frontend/design-system/foundations.md section 1.2).
 * A status keeps its role in every screen and language. Add new statuses here, never as colours in a page.
 */
export const STATUS_ROLE: Record<string, BadgeVariant> = {
  // success: done, good
  Active: 'success', Confirmed: 'success', Completed: 'success', Delivered: 'success', Captured: 'success', Sent: 'success',
  Present: 'success', Approved: 'success', Paid: 'success', Received: 'success', Accepted: 'success', RECEIVED: 'success',

  // warning: waiting for a person or partly done
  Pending: 'warning', PendingReview: 'warning', PendingApproval: 'warning', Requested: 'warning', Submitted: 'warning', SUBMITTED: 'warning',
  PartiallyReceived: 'warning', PARTIALLY_RECEIVED: 'warning', PENDING_FINANCE_APPROVAL: 'warning', Late: 'warning', HalfDay: 'warning',
  ReadyForPickup: 'warning', Refunded: 'warning', PartiallyRefunded: 'warning', ReturnRequested: 'warning', Returned: 'warning',

  // danger: failed, refused, cancelled
  Cancelled: 'danger', CANCELLED: 'danger', Failed: 'danger', Rejected: 'danger', REJECTED: 'danger', Voided: 'danger',
  DeliveryFailed: 'danger', Discontinued: 'danger', Absent: 'danger',

  // info: moving along
  Processing: 'info', Authorized: 'info', PartiallyCaptured: 'info', InTransit: 'info', PickedUp: 'info', OutForDelivery: 'info',
  Picked: 'info', Shipped: 'info', PartiallyShipped: 'info', Ordered: 'info', ORDERED: 'info', APPROVED: 'info', OnLeave: 'info',
  InProgress: 'info', Suggested: 'info', Quoting: 'info', Converted: 'info',

  // muted: not started, closed without effect
  Draft: 'muted', DRAFT: 'muted', Queued: 'muted', Holiday: 'muted', Scheduled: 'muted', Paused: 'muted', Expired: 'muted', Dismissed: 'muted',
}

/** An unknown status is muted, never an error. */
export const statusRole = (status: string): BadgeVariant => STATUS_ROLE[status] ?? 'muted'
