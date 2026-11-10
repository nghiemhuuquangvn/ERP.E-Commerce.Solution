import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { CheckCircle2, XCircle } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { mfgApi, type Order } from '@/api/manufacturing/mfg.api'
import { Alert, Button, Checkbox, Modal, Skeleton, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

interface CloseOrderDialogProps {
  order: Order
  onClose: () => void
  onClosed: (order: Order) => void
}

/** Closing an order (BR-PO-16): every condition of the checklist must be met; the issued material nobody returned may be carried to consumption. Closed is final. */
export function CloseOrderDialog({ order, onClose, onClosed }: CloseOrderDialogProps) {
  const { t } = useTranslation()
  const toast = useToast()
  const [carry, setCarry] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const { data, isLoading, isError } = useQuery({ queryKey: ['mfg', 'order', order.id, 'close-checklist', carry], queryFn: () => mfgApi.closeChecklist(order.id, carry) })
  const close = useMutation({
    mutationFn: () => mfgApi.closeOrder(order.id, carry),
    onSuccess: (closed) => { toast.success(t('mfg.close.done', { no: order.orderNo })); onClosed(closed) },
    onError: (e) => setError(serverMessage(e, t('mfg.close.failed'))),
  })

  return (
    <Modal
      open
      size="md"
      title={t('mfg.close.title', { no: order.orderNo })}
      onClose={onClose}
      closeOnEsc={!close.isPending}
      footer={<><Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button><Button variant="danger" disabled={!data?.canClose} loading={close.isPending} onClick={() => { setError(null); close.mutate() }}>{t('mfg.close.close')}</Button></>}
    >
      <div className="space-y-4">
        <p className="text-sm text-ink-secondary">{t('mfg.close.hint')}</p>
        {error && <Alert variant="danger">{error}</Alert>}
        {isError && <Alert variant="danger">{t('mfg.close.loadFailed')}</Alert>}
        {isLoading && <Skeleton className="h-24 w-full" />}
        {data && (
          <ul className="space-y-2" aria-label={t('mfg.close.conditions')}>
            {data.conditions.map((c) => (
              <li key={c.code} className="flex items-start gap-2 text-sm">
                {c.met ? <CheckCircle2 size={16} aria-hidden="true" className="mt-0.5 shrink-0 text-success" /> : <XCircle size={16} aria-hidden="true" className="mt-0.5 shrink-0 text-danger" />}
                <span><span className="font-medium text-ink">{c.met ? t('mfg.close.met') : t('mfg.close.notMet')}</span> · {c.message}</span>
              </li>
            ))}
          </ul>
        )}
        <Checkbox label={t('mfg.close.carry')} checked={carry} onChange={(e) => setCarry(e.target.checked)} />
        <p className="text-xs text-ink-secondary">{t('mfg.close.carryHint')}</p>
      </div>
    </Modal>
  )
}
