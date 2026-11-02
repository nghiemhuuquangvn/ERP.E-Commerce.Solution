import { useState, useMemo, useCallback } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { purchaseOrdersApi, PurchaseOrderStatus } from '@/api/purchaseOrders.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, EmptyState, FormField, Modal, Textarea, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'
import { format } from 'date-fns'
import type { PurchaseOrder } from '@/api/purchaseOrders.api'

const STATUS_FILTERS: { labelKey: string; value: PurchaseOrderStatus | '' }[] = [
  { labelKey: 'common.all', value: '' },
  { labelKey: 'purchaseOrders.statusFilters.draft', value: 'DRAFT' },
  { labelKey: 'purchaseOrders.statusFilters.submitted', value: 'SUBMITTED' },
  { labelKey: 'purchaseOrders.statusFilters.pendingFinanceApproval', value: 'PENDING_FINANCE_APPROVAL' },
  { labelKey: 'purchaseOrders.statusFilters.approved', value: 'APPROVED' },
  { labelKey: 'purchaseOrders.statusFilters.ordered', value: 'ORDERED' },
  { labelKey: 'purchaseOrders.statusFilters.partiallyReceived', value: 'PARTIALLY_RECEIVED' },
  { labelKey: 'purchaseOrders.statusFilters.received', value: 'RECEIVED' },
  { labelKey: 'purchaseOrders.statusFilters.rejected', value: 'REJECTED' },
  { labelKey: 'purchaseOrders.statusFilters.cancelled', value: 'CANCELLED' },
]

interface RejectFormValues {
  reason: string
}

function RejectPurchaseOrderModal({ po, onClose }: { po: PurchaseOrder; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { register, handleSubmit, formState: { errors } } = useForm<RejectFormValues>({
    defaultValues: { reason: '' },
  })

  const mutation = useMutation({
    mutationFn: (values: RejectFormValues) => purchaseOrdersApi.reject(po.id, values.reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['purchaseOrders'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('purchaseOrders.rejectTitle', { number: po.poNumber })}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="reject-po-form" variant="danger" loading={mutation.isPending}>{t('orders.reject')}</Button>
        </>
      }
    >
      <form id="reject-po-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('purchaseOrders.rejectReason')} required error={errors.reason ? t('purchaseOrders.reasonRequired') : undefined}>
          <Textarea rows={3} {...register('reason', { required: true })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('purchaseOrders.rejectFailed'))}</Alert>}
      </form>
    </Modal>
  )
}

type Step = { kind: 'approve' | 'financeApprove'; po: PurchaseOrder }

export function PurchaseOrderListPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [status, setStatus] = useState<PurchaseOrderStatus | ''>('')
  const [cancelId, setCancelId] = useState<string | null>(null)
  const [rejectTarget, setRejectTarget] = useState<PurchaseOrder | null>(null)
  const [step, setStep] = useState<Step | null>(null)
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['purchaseOrders', { page, pageSize, status }],
    queryFn: () =>
      purchaseOrdersApi.list({ pageNumber: page, pageSize, status: status || undefined }),
    staleTime: 30_000,
  })

  // the approve steps and the cancel share how they report: a toast, never silence
  const after = {
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['purchaseOrders'] })
      toast.success(t('common.saved'))
      setStep(null)
      setCancelId(null)
    },
    onError: (e: unknown) => {
      setStep(null)
      setCancelId(null)
      toast.error(serverMessage(e, t('common.error')))
    },
  }

  const approveMutation = useMutation({ mutationFn: purchaseOrdersApi.approve, ...after })
  const financeApproveMutation = useMutation({ mutationFn: purchaseOrdersApi.approveFinance, ...after })
  const cancelMutation = useMutation({ mutationFn: (id: string) => purchaseOrdersApi.cancel(id), ...after })

  const handleStatusChange = useCallback(
    (value: PurchaseOrderStatus | '') => {
      setStatus(value)
      setPage(1)
    },
    [setPage],
  )

  const handleCancelConfirm = useCallback(() => {
    if (cancelId) cancelMutation.mutate(cancelId)
  }, [cancelId, cancelMutation])

  const confirmStep = () => {
    if (!step) return
    if (step.kind === 'approve') approveMutation.mutate(step.po.id)
    else financeApproveMutation.mutate(step.po.id)
  }

  const columns = useMemo(
    () => [
      {
        key: 'poNumber',
        label: t('purchaseOrders.poNumber'),
        render: (po: PurchaseOrder) => (
          <Link to={`/orders/purchase/${po.id}`} className="text-brand-action hover:underline">
            {po.poNumber}
          </Link>
        ),
      },
      {
        key: 'supplier',
        label: t('purchaseOrders.supplier'),
        render: (po: PurchaseOrder) => po.supplier?.name ?? po.supplierId,
      },
      {
        key: 'totalAmount',
        label: t('orders.total'),
        align: 'right' as const,
        render: (po: PurchaseOrder) =>
          new Intl.NumberFormat('en-US', { style: 'currency', currency: po.currency || 'USD' }).format(
            po.totalAmount,
          ),
      },
      {
        key: 'status',
        label: t('common.status'),
        render: (po: PurchaseOrder) => <StatusBadge status={po.status} />,
      },
      {
        key: 'orderDate',
        label: t('common.date'),
        render: (po: PurchaseOrder) => format(new Date(po.orderDate), 'dd MMM yyyy'),
      },
      {
        key: 'actions',
        label: '',
        render: (po: PurchaseOrder) => (
          <div className="flex gap-3">
            {hasPermission('purchase_order:approve') && po.status === 'SUBMITTED' && (
              <Button variant="link" className="text-xs" aria-label={`${t('orders.approve')} ${po.poNumber}`} onClick={() => setStep({ kind: 'approve', po })}>
                {t('orders.approve')}
              </Button>
            )}
            {hasPermission('purchase_order:finance_approve') && po.status === 'PENDING_FINANCE_APPROVAL' && (
              <Button variant="link" className="text-xs" aria-label={`${t('purchaseOrders.financeApprove')} ${po.poNumber}`} onClick={() => setStep({ kind: 'financeApprove', po })}>
                {t('purchaseOrders.financeApprove')}
              </Button>
            )}
            {hasPermission('purchase_order:approve') &&
              ['SUBMITTED', 'PENDING_FINANCE_APPROVAL'].includes(po.status) && (
                <Button variant="link-danger" className="text-xs" aria-label={`${t('orders.reject')} ${po.poNumber}`} onClick={() => setRejectTarget(po)}>
                  {t('orders.reject')}
                </Button>
              )}
            {hasPermission('purchase_order:cancel') && !['CANCELLED', 'RECEIVED', 'REJECTED'].includes(po.status) && (
              <Button variant="link-danger" className="text-xs" aria-label={`${t('orders.cancel')} ${po.poNumber}`} onClick={() => setCancelId(po.id)}>
                {t('orders.cancel')}
              </Button>
            )}
          </div>
        ),
      },
    ],
    [hasPermission, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('purchaseOrders.title')}</h1>
        {hasPermission('purchase_order:create') && (
          <Link
            to="/orders/purchase/new"
            className="inline-flex h-9 items-center rounded-md bg-brand-action px-4 text-sm font-medium text-on-brand hover:bg-brand-action-hover"
          >
            {t('purchaseOrders.newPurchaseOrder')}
          </Link>
        )}
      </div>

      <div className="flex flex-wrap gap-1" role="group" aria-label={t('common.status')}>
        {STATUS_FILTERS.map((f) => (
          <Button
            key={f.value}
            size="sm"
            variant={status === f.value ? 'primary' : 'outline'}
            aria-pressed={status === f.value}
            onClick={() => handleStatusChange(f.value as PurchaseOrderStatus | '')}
          >
            {t(f.labelKey)}
          </Button>
        ))}
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyState={
          status ? (
            <EmptyState
              variant="no-results"
              title={t('purchaseOrders.noResults')}
              action={<Button variant="outline" size="sm" onClick={() => handleStatusChange('')}>{t('common.clear')}</Button>}
            />
          ) : undefined
        }
      />
      <Pagination
        page={page}
        pageSize={pageSize}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
      />

      <ConfirmDialog
        open={cancelId !== null}
        title={t('purchaseOrders.cancelTitle')}
        message={t('purchaseOrders.cancelMessage')}
        confirmLabel={t('orders.cancelOrderConfirm')}
        danger
        onConfirm={handleCancelConfirm}
        onCancel={() => setCancelId(null)}
      />

      <ConfirmDialog
        open={step !== null}
        title={step?.kind === 'financeApprove' ? t('purchaseOrders.financeApproveTitle') : t('purchaseOrders.approveTitle')}
        message={
          step?.kind === 'financeApprove'
            ? t('purchaseOrders.financeApproveConfirm', { number: step?.po.poNumber })
            : t('purchaseOrders.approveConfirm', { number: step?.po.poNumber })
        }
        confirmLabel={step?.kind === 'financeApprove' ? t('purchaseOrders.financeApprove') : t('orders.approve')}
        onConfirm={confirmStep}
        onCancel={() => setStep(null)}
      />

      {rejectTarget && (
        <RejectPurchaseOrderModal po={rejectTarget} onClose={() => setRejectTarget(null)} />
      )}
    </div>
  )
}
