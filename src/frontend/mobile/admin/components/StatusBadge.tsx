import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui'

export type Tone = 'neutral' | 'info' | 'success' | 'warning' | 'danger'

/**
 * Status -> tone, per domain (docs/frontend/mobile-admin-ui-elements.md section 5). The word is always shown; colour only backs it up.
 * A status from a newer server that is not listed shows its raw text in the neutral tone instead of failing.
 */
export const STATUS_TONE: Record<string, Record<string, Tone>> = {
  po: {
    DRAFT: 'neutral', SUBMITTED: 'info', ORDERED: 'info', PENDING_FINANCE_APPROVAL: 'warning', PARTIALLY_RECEIVED: 'warning',
    APPROVED: 'success', RECEIVED: 'success', REJECTED: 'danger', CANCELLED: 'danger',
  },
  so: {
    Draft: 'neutral', Submitted: 'info', Picked: 'info', Shipped: 'info', PartiallyShipped: 'info', ReturnRequested: 'warning',
    Approved: 'success', Delivered: 'success', Rejected: 'danger', Cancelled: 'danger', Returned: 'danger',
  },
  leave: { Pending: 'warning', Approved: 'success', Rejected: 'danger', Cancelled: 'neutral' },
  employee: { Active: 'success', OnLeave: 'warning', Terminated: 'neutral' },
  attendance: { Present: 'success', Late: 'warning', Absent: 'danger', HalfDay: 'warning', OnLeave: 'info', Holiday: 'neutral' },
}

export function StatusBadge({ domain, status }: { domain: keyof typeof STATUS_TONE; status: string }) {
  const { t } = useTranslation()
  const label = t(`status.${domain}.${status}`, { defaultValue: status })
  return <Badge tone={STATUS_TONE[domain][status] ?? 'neutral'} label={label} />
}
