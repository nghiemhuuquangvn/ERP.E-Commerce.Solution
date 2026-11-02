import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { paymentsApi, PaymentStatus } from '@/api/payments.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Badge, Button, Checkbox, EmptyState, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'
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

/** The payment's own currency (VND for the online gateways), not a fixed one. */
const money = (p: Payment, value: number) =>
  new Intl.NumberFormat(p.currency === 'VND' ? 'vi-VN' : 'en-US', { style: 'currency', currency: p.currency || 'USD', maximumFractionDigits: p.currency === 'VND' ? 0 : 2 }).format(value)

function RefundModal({ payment, onClose }: { payment: Payment; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const provider = payment.provider ?? payment.method
  const [amount, setAmount] = useState(String(payment.capturedAmount && payment.capturedAmount > 0 ? payment.capturedAmount : payment.amount))
  const [reason, setReason] = useState('')
  const [errors, setErrors] = useState<Partial<Record<'amount' | 'reason', string>>>({})
  const [confirming, setConfirming] = useState(false)

  const refund = useMutation({
    mutationFn: () => paymentsApi.refund(payment.id, Number(amount), reason.trim()),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['payments'] })
      toast.success(t('common.saved'))
      onClose()
    },
    onError: () => setConfirming(false),
  })

  const submit = () => {
    const found: typeof errors = {}
    const n = Number(amount)
    if (!(n >= 1 && n <= payment.amount)) found.amount = t('payments.refund.amountRange', { max: money(payment, payment.amount) })
    if (!reason.trim()) found.reason = t('common.required')
    setErrors(found)
    if (Object.keys(found).length === 0) setConfirming(true)
  }

  return (
    <>
      <Modal
        open
        size="sm"
        title={t('payments.refund.title')}
        onClose={onClose}
        closeOnEsc={!refund.isPending && !confirming}
        footer={
          <>
            <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
            <Button type="submit" form="refund-form">{t('payments.refund.submit')}</Button>
          </>
        }
      >
        <form id="refund-form" noValidate onSubmit={(e) => { e.preventDefault(); submit() }} className="space-y-4">
          <p className="text-sm text-ink-secondary">{t('payments.refund.hint', { provider })}</p>
          <FormField label={t('payments.refund.amount')} required hint={t('payments.refund.maxHint', { max: money(payment, payment.amount) })} error={errors.amount}>
            <Input type="number" min={1} max={payment.amount} value={amount} onChange={(e) => setAmount(e.target.value)} />
          </FormField>
          <FormField label={t('payments.refund.reason')} required error={errors.reason}>
            <Input value={reason} onChange={(e) => setReason(e.target.value)} />
          </FormField>
          {refund.isError && <Alert variant="danger">{serverMessage(refund.error, t('common.failedToSave'))}</Alert>}
        </form>
      </Modal>
      <ConfirmDialog
        open={confirming}
        title={t('payments.refund.confirmTitle')}
        message={
          provider === 'COD'
            ? t('payments.refund.confirmCod', { amount: money(payment, Number(amount)) })
            : t('payments.refund.confirmMessage', { amount: money(payment, Number(amount)), provider })
        }
        confirmLabel={t('payments.refund.confirmAction')}
        danger
        onConfirm={() => refund.mutate()}
        onCancel={() => setConfirming(false)}
      />
    </>
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

  const canRefund = hasPermission('payments:refund')
  const filtering = !!(status || provider || needsReview)
  const clear = () => {
    setStatus('')
    setProvider('')
    setNeedsReview(false)
    setPage(1)
  }

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
          {p.provider && <div className="text-xs text-ink-secondary">{p.method}</div>}
        </div>
      ),
    },
    {
      key: 'amount',
      label: t('payments.amount'),
      align: 'right' as const,
      render: (p: Payment) => (
        <div>
          <div>{money(p, p.amount)}</div>
          {p.status === 'PartiallyCaptured' && (
            <div className="text-xs text-info-soft-text">
              {t('payments.collectedOf', { collected: new Intl.NumberFormat('vi-VN').format(p.capturedAmount ?? 0), currency: p.currency })}
            </div>
          )}
        </div>
      ),
    },
    {
      key: 'status',
      label: t('common.status'),
      render: (p: Payment) => (
        <div className="space-y-1">
          <StatusBadge status={p.status} />
          {p.isFlaggedForReview && (
            <div>
              <Badge variant="warning">{t('payments.flagged')}</Badge>
              {p.fraudReviewReason && <span className="ml-1 text-xs text-ink-secondary">{p.fraudReviewReason}</span>}
            </div>
          )}
          {p.failureReason && <div className="text-xs text-ink-secondary">{p.failureReason}</div>}
        </div>
      ),
    },
    { key: 'createdAt', label: t('common.date'), render: (p: Payment) => format(new Date(p.createdAt), 'dd MMM yyyy') },
    {
      key: 'actions',
      label: '',
      render: (p: Payment) =>
        canRefund && (p.status === 'Captured' || p.status === 'PartiallyRefunded' || p.status === 'PartiallyCaptured') ? (
          <Button variant="outline" size="sm" aria-label={`${t('payments.refund.button')} ${p.orderNumber ?? p.id.slice(0, 8)}`} onClick={() => setRefunding(p)}>
            {t('payments.refund.button')}
          </Button>
        ) : null,
    },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('payments.title')}</h1>

      <div className="flex flex-wrap gap-1" role="group" aria-label={t('common.status')}>
        {STATUS_FILTERS.map((f) => (
          <Button
            key={f.value}
            size="sm"
            variant={status === f.value ? 'primary' : 'outline'}
            aria-pressed={status === f.value}
            onClick={() => { setStatus(f.value as PaymentStatus | ''); setPage(1) }}
          >
            {t(f.labelKey)}
          </Button>
        ))}
      </div>

      <div className="flex flex-wrap items-end gap-4">
        <FormField label={t('payments.provider')}>
          <Select className="w-auto" value={provider} onChange={(e) => { setProvider(e.target.value); setPage(1) }}>
            <option value="">{t('common.all')}</option>
            {PROVIDERS.map((p) => <option key={p} value={p}>{p}</option>)}
          </Select>
        </FormField>
        <Checkbox label={t('payments.needsReview')} checked={needsReview} onChange={(e) => { setNeedsReview(e.target.checked); setPage(1) }} />
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyState={
          filtering ? (
            <EmptyState variant="no-results" title={t('common.noResults')} action={<Button variant="outline" size="sm" onClick={clear}>{t('common.clear')}</Button>} />
          ) : (
            <EmptyState title={t('payments.empty')} />
          )
        }
      />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {refunding && <RefundModal payment={refunding} onClose={() => setRefunding(null)} />}
    </div>
  )
}
