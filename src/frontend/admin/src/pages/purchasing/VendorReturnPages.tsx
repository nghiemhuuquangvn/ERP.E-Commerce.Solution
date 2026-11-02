import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { procurementApi } from '@/api/purchasing/procurement.api'
import type { VendorReturnSummary } from '@/api/purchasing/procurement.api'
import { purchaseOrdersApi } from '@/api/purchaseOrders.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Button, EmptyState, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

const money = (v: number) => new Intl.NumberFormat().format(v)

function CreateReturnModal({ presetOrderId, onClose }: { presetOrderId?: string; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [orderId, setOrderId] = useState(presetOrderId ?? '')
  const [reason, setReason] = useState('')
  const [qty, setQty] = useState<Record<string, string>>({})
  const [errors, setErrors] = useState<Partial<Record<'order' | 'reason' | 'qty', string>>>({})
  const { data: orders } = useQuery({ queryKey: ['po', 'orders', 'returnable'], queryFn: () => purchaseOrdersApi.list({ pageNumber: 1, pageSize: 100 }) })
  const received = (orders?.items ?? []).filter((o) => /received/i.test(String(o.status).replace(/_/g, '')))
  const { data: proc } = useQuery({ queryKey: ['po', 'procurement', orderId], queryFn: () => procurementApi.orderProcurement(orderId), enabled: !!orderId })

  const create = useMutation({
    mutationFn: () => procurementApi.createReturn({
      purchaseOrderId: orderId, reason,
      lines: Object.entries(qty).filter(([, v]) => Number(v) > 0).map(([productCode, v]) => ({ productCode, quantity: Number(v) })),
    }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['po', 'returns'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  const submit = () => {
    const found: typeof errors = {}
    if (!orderId) found.order = t('common.required')
    if (!reason.trim()) found.reason = t('common.required')
    const tooMany = proc?.lines.some((l) => Number(qty[l.productCode]) > l.returnable)
    if (tooMany) found.qty = t('procurement.receive.exceeds')
    else if (!Object.values(qty).some((v) => Number(v) > 0)) found.qty = t('procurement.return.noQuantity')
    setErrors(found)
    if (Object.keys(found).length === 0) create.mutate()
  }

  return (
    <Modal
      open
      size="lg"
      title={t('procurement.return.newTitle')}
      onClose={onClose}
      closeOnEsc={!create.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="vendor-return-form" loading={create.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="vendor-return-form" noValidate onSubmit={(e) => { e.preventDefault(); submit() }} className="space-y-4">
        <FormField label={t('procurement.invoice.order')} required error={errors.order} hint={received.length === 0 ? t('procurement.return.noOrders') : undefined}>
          <Select value={orderId} onChange={(e) => { setOrderId(e.target.value); setQty({}) }}>
            <option value="">{t('common.select')}</option>
            {received.map((o) => <option key={o.id} value={o.id}>{o.poNumber} — {o.supplier?.name ?? ''}</option>)}
          </Select>
        </FormField>

        {proc && (
          <div>
            <table className="min-w-full text-sm">
              <thead>
                <tr className="border-b border-line text-left text-xs uppercase text-ink-secondary">
                  <th className="pb-2">{t('procurement.productName')}</th>
                  <th className="pb-2 text-right">{t('procurement.received')}</th>
                  <th className="pb-2 text-right">{t('procurement.returned')}</th>
                  <th className="pb-2 text-right">{t('procurement.return.returnable')}</th>
                  <th className="pb-2 text-right">{t('procurement.return.quantity')}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {proc.lines.map((l) => (
                  <tr key={l.productCode}>
                    <td className="py-2">{l.productName}</td>
                    <td className="py-2 text-right tabular-nums">{l.received}</td>
                    <td className="py-2 text-right tabular-nums">{l.returned}</td>
                    <td className="py-2 text-right font-semibold tabular-nums">{l.returnable}</td>
                    <td className="py-2 text-right">
                      <Input
                        type="number"
                        min={0}
                        max={l.returnable}
                        aria-label={`${t('procurement.return.quantity')}: ${l.productName}`}
                        aria-invalid={Number(qty[l.productCode]) > l.returnable || undefined}
                        disabled={l.returnable === 0}
                        className="ml-auto w-24 text-right"
                        value={qty[l.productCode] ?? ''}
                        onChange={(e) => setQty({ ...qty, [l.productCode]: e.target.value })}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {errors.qty && <p role="alert" className="mt-1 text-xs text-danger-soft-text">{errors.qty}</p>}
          </div>
        )}

        <FormField label={t('procurement.return.reason')} required error={errors.reason}>
          <Input value={reason} onChange={(e) => setReason(e.target.value)} />
        </FormField>
        {create.isError && <Alert variant="danger">{serverMessage(create.error, t('common.error'))}</Alert>}
      </form>
    </Modal>
  )
}

function CreditModal({ ret, onClose }: { ret: VendorReturnSummary; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [number, setNumber] = useState('')
  const [amount, setAmount] = useState(String(ret.value))
  const [errors, setErrors] = useState<Partial<Record<'number' | 'amount', string>>>({})
  const credit = useMutation({
    mutationFn: () => procurementApi.creditReturn(ret.id, number, Number(amount)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['po', 'returns'] })
      toast.success(t('common.saved'))
      onClose()
    },
  })

  const submit = () => {
    const found: typeof errors = {}
    if (!number.trim()) found.number = t('common.required')
    if (!(Number(amount) > 0)) found.amount = t('common.required')
    setErrors(found)
    if (Object.keys(found).length === 0) credit.mutate()
  }

  return (
    <Modal
      open
      size="sm"
      title={t('procurement.return.creditTitle')}
      onClose={onClose}
      closeOnEsc={!credit.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="vendor-credit-form" loading={credit.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="vendor-credit-form" noValidate onSubmit={(e) => { e.preventDefault(); submit() }} className="space-y-4">
        <p className="text-sm text-ink-secondary">{ret.returnNumber} · {money(ret.value)} {ret.currencyCode}</p>
        <FormField label={t('procurement.return.creditNote')} required error={errors.number}>
          <Input value={number} onChange={(e) => setNumber(e.target.value)} />
        </FormField>
        <FormField label={t('procurement.return.creditAmount')} required error={errors.amount}>
          <Input type="number" value={amount} onChange={(e) => setAmount(e.target.value)} />
        </FormField>
        {credit.isError && <Alert variant="danger">{serverMessage(credit.error, t('common.error'))}</Alert>}
      </form>
    </Modal>
  )
}

type Step = { kind: 'ship' | 'cancel'; ret: VendorReturnSummary }

export function VendorReturnListPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [params] = useSearchParams()
  const purchaseOrderId = params.get('po') ?? undefined
  const [status, setStatus] = useState('')
  const [creating, setCreating] = useState(false)
  const [crediting, setCrediting] = useState<VendorReturnSummary | null>(null)
  const [step, setStep] = useState<Step | null>(null)
  const canManage = hasPermission('vendor_return:manage')

  const { data = [], isLoading } = useQuery({ queryKey: ['po', 'returns', status, purchaseOrderId], queryFn: () => procurementApi.returns({ status: status || undefined, purchaseOrderId }) })

  const after = {
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['po', 'returns'] })
      toast.success(t('common.saved'))
      setStep(null)
    },
    onError: (e: unknown) => {
      setStep(null)
      toast.error(serverMessage(e, t('common.error')))
    },
  }
  const ship = useMutation({ mutationFn: (id: string) => procurementApi.shipReturn(id), ...after })
  const cancel = useMutation({ mutationFn: (id: string) => procurementApi.cancelReturn(id), ...after })

  const columns = useMemo(
    () => [
      { key: 'returnNumber', label: t('procurement.return.number'), className: 'font-mono text-xs' },
      { key: 'poNumber', label: t('procurement.invoice.order') },
      { key: 'supplierName', label: t('procurement.supplier') },
      { key: 'value', label: t('procurement.return.value'), align: 'right' as const, render: (r: VendorReturnSummary) => `${money(r.value)} ${r.currencyCode}` },
      { key: 'status', label: t('common.status'), render: (r: VendorReturnSummary) => <StatusBadge status={r.status} /> },
      {
        key: 'actions', label: '',
        render: (r: VendorReturnSummary) => canManage ? (
          <div className="flex gap-3">
            {r.status === 'Draft' && (
              <Button variant="link" className="text-xs" aria-label={`${t('procurement.return.ship')} ${r.returnNumber}`} onClick={() => setStep({ kind: 'ship', ret: r })}>
                {t('procurement.return.ship')}
              </Button>
            )}
            {r.status === 'Draft' && (
              <Button variant="link-danger" className="text-xs" aria-label={`${t('common.cancel')} ${r.returnNumber}`} onClick={() => setStep({ kind: 'cancel', ret: r })}>
                {t('common.cancel')}
              </Button>
            )}
            {r.status === 'Shipped' && (
              <Button variant="link" className="text-xs" aria-label={`${t('procurement.return.recordCredit')} ${r.returnNumber}`} onClick={() => setCrediting(r)}>
                {t('procurement.return.recordCredit')}
              </Button>
            )}
          </div>
        ) : null,
      },
    ],
    [canManage, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">{t('procurement.return.listTitle')}</h1>
          <p className="text-sm text-ink-secondary">{t('procurement.return.hint')}</p>
        </div>
        {canManage && <Button onClick={() => setCreating(true)}>{t('procurement.return.add')}</Button>}
      </div>
      <Select aria-label={t('common.status')} className="max-w-xs" value={status} onChange={(e) => setStatus(e.target.value)}>
        <option value="">{t('procurement.allStatuses')}</option>
        {['Draft', 'Shipped', 'Credited', 'Cancelled'].map((s) => <option key={s} value={s}>{s}</option>)}
      </Select>
      <DataTable
        columns={columns}
        data={data}
        isLoading={isLoading}
        emptyState={
          status ? (
            <EmptyState variant="no-results" title={t('common.noResults')} action={<Button variant="outline" size="sm" onClick={() => setStatus('')}>{t('common.clear')}</Button>} />
          ) : (
            <EmptyState title={t('procurement.return.empty')} />
          )
        }
      />
      {creating && <CreateReturnModal presetOrderId={purchaseOrderId} onClose={() => setCreating(false)} />}
      {crediting && <CreditModal ret={crediting} onClose={() => setCrediting(null)} />}

      <ConfirmDialog
        open={step !== null}
        title={step?.kind === 'ship' ? t('procurement.return.shipTitle') : t('procurement.return.cancelTitle')}
        message={step?.kind === 'ship' ? t('procurement.return.shipConfirm', { number: step?.ret.returnNumber }) : t('procurement.return.cancelConfirm', { number: step?.ret.returnNumber })}
        confirmLabel={step?.kind === 'ship' ? t('procurement.return.ship') : t('procurement.return.cancelTitle')}
        danger={step?.kind === 'cancel'}
        onConfirm={() => step && (step.kind === 'ship' ? ship.mutate(step.ret.id) : cancel.mutate(step.ret.id))}
        onCancel={() => setStep(null)}
      />
    </div>
  )
}
