import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import axios from 'axios'
import { format } from 'date-fns'
import { useTranslation } from 'react-i18next'
import { Check, Circle, CircleDot, X } from 'lucide-react'
import { orderSagaApi } from '@/api/orderSaga.api'
import type { OrderSagaStep } from '@/api/orderSaga.api'
import { usePermission } from '@/hooks/usePermission'

const STATE_STYLE: Record<OrderSagaStep['state'], string> = {
  Done: 'bg-success-soft text-success-soft-text border-success/30',
  Current: 'bg-info-soft text-info-soft-text border-brand-action ring-2 ring-info/30',
  Waiting: 'bg-surface-muted text-ink-muted border-line',
  Failed: 'bg-danger-soft text-danger-soft-text border-danger',
}

const STATE_MARK: Record<OrderSagaStep['state'], JSX.Element> = {
  Done: <Check size={14} strokeWidth={1.75} aria-hidden="true" />,
  Current: <CircleDot size={14} strokeWidth={1.75} aria-hidden="true" />,
  Waiting: <Circle size={14} strokeWidth={1.75} aria-hidden="true" />,
  Failed: <X size={14} strokeWidth={1.75} aria-hidden="true" />,
}

/** Minutes between two moments, shown as "12 min" / "3 h 5 min". */
function waited(from: string, to: Date) {
  const minutes = Math.max(0, Math.floor((to.getTime() - new Date(from).getTime()) / 60_000))
  return minutes < 60 ? `${minutes} min` : `${Math.floor(minutes / 60)} h ${minutes % 60} min`
}

/**
 * "Order processing" card of the order page (ROADMAP P1-5): the steps the order goes through after it is placed (payment, stock reservation, approval, shipment), where it is
 * now, how long that step has waited, how many times the system has already pushed it again, the error, and a button to push the stuck step by hand.
 */
export function OrderSagaPanel({ orderId }: { orderId: string }) {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const key = ['orders', orderId, 'saga']

  const { data: saga } = useQuery({
    queryKey: key,
    queryFn: () => orderSagaApi.get(orderId),
    // refreshed while the order is still moving
    refetchInterval: (query) => (query.state.data?.status === 'Running' ? 15_000 : false),
  })
  const retry = useMutation({
    mutationFn: () => orderSagaApi.retry(orderId),
    onSuccess: (result) => queryClient.setQueryData(key, result),
  })

  if (!saga || !saga.exists) return null

  const error = axios.isAxiosError(retry.error) ? ((retry.error.response?.data as { message?: string; detail?: string })?.detail ?? (retry.error.response?.data as { message?: string })?.message) : undefined
  const failed = saga.status === 'Failed' || saga.status === 'Compensating'
  const delivery = saga.status === 'Completed' && saga.message?.startsWith('Delivery failed')

  return (
    <div className="card p-5" data-testid="order-saga">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-ink-muted">{t('orders.saga.title')}</h2>
        <span className="text-xs text-ink-muted">{t(`orders.saga.status.${saga.status}`)}</span>
      </div>

      <ol className="flex flex-wrap items-center gap-2 text-sm">
        {saga.steps.map((s, i) => (
          <li key={s.key} className="flex items-center gap-2">
            <span className={`inline-flex items-center gap-1.5 rounded-full border px-3 py-1 ${STATE_STYLE[s.state]}`} aria-current={s.state === 'Current' ? 'step' : undefined}>
              {STATE_MARK[s.state]}
              {t(`orders.saga.step.${s.key}`)}
            </span>
            {i < saga.steps.length - 1 && <span className="text-ink-muted">→</span>}
          </li>
        ))}
      </ol>

      {saga.status === 'Running' && (
        <div className="mt-3 space-y-1 text-sm">
          {saga.timeoutMinutes === null ? (
            <p className="text-ink-secondary">{t('orders.saga.waitingForPerson')}</p>
          ) : (
            <p className={saga.stuck ? 'text-warning-soft-text' : 'text-ink-secondary'}>
              {t('orders.saga.waitingSince', { step: t(`orders.saga.step.${saga.steps.find((s) => s.state === 'Current')?.key ?? 'Done'}`), time: waited(saga.lastUpdatedAt, new Date()), limit: saga.timeoutMinutes })}
              {saga.stuck && ` — ${t('orders.saga.stuck')}`}
            </p>
          )}
          {saga.retryCount > 0 && <p className="text-xs text-ink-muted">{t('orders.saga.retries', { count: saga.retryCount, max: saga.maxRetries })}</p>}
          {saga.canRetry && hasPermission('sales_order:update') && (
            <button className="btn-outline mt-1 text-sm" disabled={retry.isPending} onClick={() => retry.mutate()}>
              {retry.isPending ? t('common.saving') : t('orders.saga.retry')}
            </button>
          )}
          {retry.isError && <p className="text-xs text-danger-soft-text">{error ?? t('orders.saga.retryFailed')}</p>}
        </div>
      )}

      {failed && (
        <div className="mt-3 rounded border border-danger/30 bg-danger-soft p-3 text-sm text-danger-soft-text" role="alert">
          <p className="font-medium">{t('orders.saga.cancelledAutomatically')}</p>
          {saga.message && <p className="mt-1 text-xs">{saga.message}</p>}
        </div>
      )}

      {saga.status === 'Completed' && !delivery && saga.completedAt && (
        <p className="mt-3 text-xs text-ink-muted">{t('orders.saga.completedAt', { date: format(new Date(saga.completedAt), 'dd MMM yyyy HH:mm') })}</p>
      )}
      {delivery && (
        <p className="mt-3 rounded border border-warning/30 bg-warning-soft p-2 text-xs text-warning-soft-text" role="status">
          {saga.message}
        </p>
      )}
    </div>
  )
}
