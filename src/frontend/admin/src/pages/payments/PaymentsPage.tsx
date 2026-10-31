import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { paymentsApi, PaymentStatus } from '@/api/payments.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { serverMessage } from '@/api/purchasing/procurement.api'
import { format } from 'date-fns'
import type { Payment } from '@/api/payments.api'

const STATUS_FILTERS: { labelKey: string; value: PaymentStatus | '' }[] = [
  { labelKey: 'common.all', value: '' },
  { labelKey: 'payments.statusFilters.pending', value: 'Pending' },
  { labelKey: 'payments.statusFilters.captured', value: 'Captured' },
  { labelKey: 'payments.statusFilters.partiallyCaptured', value: 'PartiallyCaptured' },
  { labelKey: 'payments.statusFilters.failed', value: 'Failed' },
  { labelKey: 'payments.statusFilters.refunded', value: 'Refunded' },
  { labelKey: 'payments.statusFilters.partiallyRefunded', value: 'PartiallyRefunded' },
]

const PROVIDERS = ['COD', 'VNPay', 'MoMo', 'ZaloPay']

function RefundModal({ payment, onClose }: { payment: Payment; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [amount, setAmount] = useState(payment.capturedAmount && payment.capturedAmount > 0 ? payment.capturedAmount : payment.amount)
  const [reason, setReason] = useState('')

  const refund = useMutation({
    mutationFn: () => paymentsApi.refund(payment.id, amount, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['payments'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md space-y-4 p-6">
        <h3 className="text-base font-semibold text-gray-900">{t('payments.refund.title')}</h3>
        <p className="text-xs text-gray-500">{t('payments.refund.hint', { provider: payment.provider ?? payment.method })}</p>
        <label className="block text-sm font-medium text-gray-700">
          {t('payments.refund.amount')}
          <input type="number" min={1} max={payment.amount} className="input mt-1" value={amount} onChange={(e) => setAmount(Number(e.target.value))} />
        </label>
        <label className="block text-sm font-medium text-gray-700">
          {t('payments.refund.reason')}
          <input className="input mt-1" value={reason} onChange={(e) => setReason(e.target.value)} />
        </label>
        {refund.isError && <p className="text-xs text-red-600">{serverMessage(refund.error, t('common.failedToSave'))}</p>}
        <div className="flex justify-end gap-3">
          <button className="btn-outline" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn-primary" disabled={refund.isPending || amount <= 0 || amount > payment.amount || !reason.trim()} onClick={() => refund.mutate()}>
            {refund.isPending ? t('common.saving') : t('payments.refund.submit')}
          </button>
        </div>
      </div>
    </div>
  )
}

export function PaymentsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [status, setStatus] = useState<PaymentStatus | ''>('')
  const [provider, setProvider] = useState('')
  const [needsReview, setNeedsReview] = useState(false)
  const [refunding, setRefunding] = useState<Payment | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['payments', { page, pageSize, status, provider, needsReview }],
    queryFn: () => paymentsApi.list({ page, pageSize, status: status || undefined, provider: provider || undefined, needsReview: needsReview || undefined }),
  })

  // The payment's own currency (VND for the online gateways), not a fixed one.
  const fmt = (p: Payment) =>
    new Intl.NumberFormat(p.currency === 'VND' ? 'vi-VN' : 'en-US', { style: 'currency', currency: p.currency || 'USD', maximumFractionDigits: p.currency === 'VND' ? 0 : 2 }).format(p.amount)

  const canRefund = hasPermission('payments:refund')

  const columns = [
    { key: 'id', label: 'ID', className: 'font-mono text-xs', render: (p: Payment) => p.id.slice(0, 8) },
    {
      key: 'orderId',
      label: t('payments.order'),
      className: 'font-mono text-xs',
      render: (p: Payment) => p.orderNumber ?? p.orderId.slice(0, 8),
    },
    {
      key: 'provider',
      label: t('payments.method'),
      render: (p: Payment) => (
        <div>
          <div>{p.provider ?? p.method}</div>
          {p.provider && <div className="text-xs text-gray-400">{p.method}</div>}
        </div>
      ),
    },
    {
      key: 'amount',
      label: t('payments.amount'),
      render: (p: Payment) => (
        <div>
          <div>{fmt(p)}</div>
          {p.status === 'PartiallyCaptured' && (
            <div className="text-xs text-blue-700">
              {t('payments.collectedOf', { collected: new Intl.NumberFormat('vi-VN').format(p.capturedAmount ?? 0), currency: p.currency })}
            </div>
          )}
        </div>
      ),
      className: 'text-right',
    },
    {
      key: 'status',
      label: t('common.status'),
      render: (p: Payment) => (
        <div className="space-y-1">
          <StatusBadge status={p.status} />
          {p.isFlaggedForReview && (
            <div className="text-xs text-amber-700" title={p.fraudReviewReason ?? ''}>{t('payments.flagged')}</div>
          )}
          {p.failureReason && <div className="text-xs text-gray-400">{p.failureReason}</div>}
        </div>
      ),
    },
    { key: 'createdAt', label: t('common.date'), render: (p: Payment) => format(new Date(p.createdAt), 'dd MMM yyyy') },
    {
      key: 'actions',
      label: '',
      className: 'text-right',
      render: (p: Payment) =>
        canRefund && (p.status === 'Captured' || p.status === 'PartiallyRefunded' || p.status === 'PartiallyCaptured') ? (
          <button className="btn-outline text-xs" onClick={() => setRefunding(p)}>{t('payments.refund.button')}</button>
        ) : null,
    },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('payments.title')}</h1>

      <div className="flex flex-wrap gap-1">
        {STATUS_FILTERS.map((f) => (
          <button
            key={f.value}
            onClick={() => { setStatus(f.value as PaymentStatus | ''); setPage(1) }}
            className={`rounded px-3 py-1.5 text-sm ${
              status === f.value ? 'bg-primary-600 text-white' : 'bg-white border border-gray-300 text-gray-600 hover:bg-gray-50'
            }`}
          >
            {t(f.labelKey)}
          </button>
        ))}
      </div>

      <div className="flex flex-wrap items-center gap-4">
        <label className="flex items-center gap-2 text-sm text-gray-600">
          {t('payments.provider')}
          <select className="input w-auto" value={provider} onChange={(e) => { setProvider(e.target.value); setPage(1) }}>
            <option value="">{t('common.all')}</option>
            {PROVIDERS.map((p) => <option key={p} value={p}>{p}</option>)}
          </select>
        </label>
        <label className="flex items-center gap-2 text-sm text-gray-600">
          <input type="checkbox" checked={needsReview} onChange={(e) => { setNeedsReview(e.target.checked); setPage(1) }} />
          {t('payments.needsReview')}
        </label>
      </div>

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {refunding && <RefundModal payment={refunding} onClose={() => setRefunding(null)} />}
    </div>
  )
}
