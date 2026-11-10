import { useTranslation } from 'react-i18next'
import { AlertTriangle } from 'lucide-react'
import type { OperationStatus, OrderStatus, StockDocumentStatus, StockDocumentType, StockLineStatus } from '@/api/manufacturing/mfg.api'
import { Badge } from '@/components/ui'
import { statusRole } from '@/components/ui/statusRoles'

export function OrderStatusBadge({ status }: { status: OrderStatus }) {
  const { t } = useTranslation()
  return <Badge variant={statusRole(status)}>{t(`mfg.orderStatus.${status}`)}</Badge>
}

export function OperationStatusBadge({ status }: { status: OperationStatus }) {
  const { t } = useTranslation()
  return <Badge variant={statusRole(status)}>{t(`mfg.opStatus.${status}`)}</Badge>
}

export function DocStatusBadge({ status }: { status: StockDocumentStatus | StockLineStatus | 'Stopped' | 'Running' | 'Paused' }) {
  const { t } = useTranslation()
  return <Badge variant={statusRole(status)}>{t(`mfg.docStatus.${status}`)}</Badge>
}

export function DocTypeLabel({ type }: { type: StockDocumentType }) {
  const { t } = useTranslation()
  return <span>{t(`mfg.docType.${type}`)}</span>
}

/** The late flag is text with an icon, never colour alone. */
export function LateFlag({ late }: { late: boolean }) {
  const { t } = useTranslation()
  if (!late) return null
  return <Badge variant="danger"><AlertTriangle size={12} aria-hidden="true" className="mr-1 inline" />{t('mfg.order.late')}</Badge>
}
