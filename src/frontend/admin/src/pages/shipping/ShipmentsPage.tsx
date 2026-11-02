import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { format } from 'date-fns'
import { shippingApi, type Shipment, type SplitPart } from '@/api/shipping.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import { Alert, Badge, Button, Checkbox, EmptyState, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

const STATUS_FILTERS = ['', 'Pending', 'ReadyForPickup', 'PickedUp', 'InTransit', 'OutForDelivery', 'Delivered', 'DeliveryFailed', 'Returned', 'Cancelled']

const money = (n: number) => `${new Intl.NumberFormat('vi-VN').format(n)} ₫`

/** Splits a parcel the carrier has not collected into several: a grid of product quantities per new parcel (every item must go to exactly one parcel). */
function SplitDialog({ shipment, onClose, onDone }: { shipment: Shipment; onClose: () => void; onDone: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const items = shipment.items ?? []
  const [count, setCount] = useState(2)
  // qty[parcel][item]; the first parcel starts with everything so the person moves items out.
  const [qty, setQty] = useState<number[][]>(() => [items.map((i) => i.quantity), items.map(() => 0)])
  const [cod, setCod] = useState<number[]>([])
  const [confirming, setConfirming] = useState(false)

  const resize = (n: number) => {
    setCount(n)
    setQty((q) => Array.from({ length: n }, (_, p) => q[p] ?? items.map(() => 0)))
    setCod([])
  }
  const setCell = (p: number, i: number, v: number) => {
    setQty((q) => q.map((row, rp) => (rp === p ? row.map((x, ri) => (ri === i ? Math.max(0, v) : x)) : row)))
    setCod([])
  }

  const units = qty.map((row) => row.reduce((a, b) => a + b, 0))
  const totalUnits = units.reduce((a, b) => a + b, 0)
  const unassigned = items.map((it, i) => it.quantity - qty.reduce((a, row) => a + row[i], 0))
  const missing = unassigned.filter((u) => u > 0).reduce((a, b) => a + b, 0)
  const extra = unassigned.filter((u) => u < 0).reduce((a, b) => a - b, 0)
  const emptyParcel = units.some((u) => u === 0)
  const exact = missing === 0 && extra === 0 && !emptyParcel
  // The proposed cash follows the units; the last parcel takes the remainder (the server does the same when none is typed).
  const proposal = qty.map((_, p) => (p === count - 1 ? shipment.codAmount - qty.slice(0, p).reduce((a, _r, rp) => a + Math.round((shipment.codAmount * units[rp]) / Math.max(1, totalUnits)), 0) : Math.round((shipment.codAmount * units[p]) / Math.max(1, totalUnits))))
  const codValues = shipment.codAmount > 0 ? (cod.length === count ? cod : proposal) : []
  const codSum = codValues.reduce((a, b) => a + b, 0)
  const codOk = shipment.codAmount === 0 || codSum === shipment.codAmount

  const split = useMutation({
    mutationFn: () => {
      const parts: SplitPart[] = qty.map((row, p) => ({
        items: items.map((it, i) => ({ productId: it.productId, quantity: row[i] })).filter((l) => l.quantity > 0),
        codAmount: shipment.codAmount > 0 ? codValues[p] : undefined,
      }))
      return shippingApi.split(shipment.id, parts)
    },
    onSuccess: () => {
      toast.success(t('common.saved'))
      onDone()
    },
    onError: () => setConfirming(false),
  })

  const problem =
    missing > 0 ? t('shipments.split.unassigned', { count: missing })
    : extra > 0 ? t('shipments.split.overAssigned', { count: extra })
    : emptyParcel ? t('shipments.split.emptyParcel')
    : !codOk ? t('shipments.split.codOff', { total: money(shipment.codAmount) })
    : null

  return (
    <>
      <Modal
        open
        size="lg"
        title={t('shipments.split.title')}
        onClose={onClose}
        closeOnEsc={!split.isPending && !confirming}
        footer={
          <>
            <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
            <Button disabled={!exact || !codOk} onClick={() => setConfirming(true)}>{t('shipments.split.confirm')}</Button>
          </>
        }
      >
        <div className="space-y-4">
          <p className="text-xs text-ink-secondary">{t('shipments.split.hint')}</p>
          <FormField label={t('shipments.split.parcels')} className="w-40">
            <Input type="number" min={2} max={10} value={count} onChange={(e) => resize(Math.min(10, Math.max(2, Number(e.target.value) || 2)))} />
          </FormField>
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-xs text-ink-secondary">
                <th className="py-1">{t('shipments.split.item')}</th>
                {qty.map((_, p) => <th key={p} className="w-24 py-1">{t('shipments.split.parcel', { n: p + 1 })}</th>)}
                <th className="w-24 py-1">{t('shipments.split.left')}</th>
              </tr>
            </thead>
            <tbody>
              {items.map((it, i) => (
                <tr key={it.productId} className="border-t border-line">
                  <td className="py-1">{it.productName} <span className="text-xs text-ink-secondary">× {it.quantity}</span></td>
                  {qty.map((row, p) => (
                    <td key={p}>
                      <Input
                        type="number"
                        min={0}
                        className="w-20"
                        aria-label={t('shipments.itemParcel', { item: it.productName, n: p + 1 })}
                        value={row[i]}
                        onChange={(e) => setCell(p, i, Number(e.target.value) || 0)}
                      />
                    </td>
                  ))}
                  <td className={unassigned[i] === 0 ? 'text-ink-secondary' : 'font-medium text-danger-soft-text'}>{unassigned[i]}</td>
                </tr>
              ))}
              {shipment.codAmount > 0 && (
                <tr className="border-t border-line-strong">
                  <td className="py-1 font-medium">{t('shipments.cod')}</td>
                  {codValues.map((v, p) => (
                    <td key={p}>
                      <Input
                        type="number"
                        min={0}
                        className="w-24"
                        aria-label={t('shipments.codParcel', { n: p + 1 })}
                        value={v}
                        onChange={(e) => setCod(codValues.map((x, xp) => (xp === p ? Number(e.target.value) || 0 : x)))}
                      />
                    </td>
                  ))}
                  <td className={codOk ? 'text-ink-secondary' : 'font-medium text-danger-soft-text'}>{money(shipment.codAmount - codSum)}</td>
                </tr>
              )}
            </tbody>
          </table>
          <p className={problem ? 'text-sm font-medium text-danger-soft-text' : 'text-sm text-success-soft-text'} aria-live="polite">
            {problem ?? t('shipments.split.allAssigned')}
          </p>
          {split.isError && <Alert variant="danger">{serverMessage(split.error, t('common.failedToSave'))}</Alert>}
        </div>
      </Modal>
      <ConfirmDialog
        open={confirming}
        title={t('shipments.split.confirmTitle')}
        message={t('shipments.split.confirmMessage', { count })}
        confirmLabel={t('shipments.split.confirm')}
        danger
        onConfirm={() => split.mutate()}
        onCancel={() => setConfirming(false)}
      />
    </>
  )
}

type Step = 'rebook' | 'cancel'

function ShipmentDetail({ shipment, onClose }: { shipment: Shipment; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const { hasPermission } = usePermission()
  const canUpdate = hasPermission('shipping:update')
  const [step, setStep] = useState<Step | null>(null)
  const [splitting, setSplitting] = useState(false)

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['shipments'] })
  const after = {
    onSuccess: () => {
      refresh()
      toast.success(t('common.saved'))
      setStep(null)
      onClose()
    },
    onError: (e: unknown) => {
      setStep(null)
      toast.error(serverMessage(e, t('common.failedToSave')))
    },
  }
  const cancel = useMutation({ mutationFn: () => shippingApi.cancelAtCarrier(shipment.id), ...after })
  const rebook = useMutation({ mutationFn: () => shippingApi.rebook(shipment.id), ...after })

  const splittable = canUpdate && ['Pending', 'ReadyForPickup'].includes(shipment.status) && (shipment.items ?? []).reduce((a, i) => a + i.quantity, 0) > 1
  const cancellable = canUpdate && ['Pending', 'ReadyForPickup'].includes(shipment.status)
  const canRebook = canUpdate && !shipment.trackingNumber && shipment.status !== 'Cancelled'
  const row = (label: string, value?: string | number | null) =>
    value === undefined || value === null || value === '' ? null : (
      <div className="flex justify-between gap-4 py-1 text-sm">
        <dt className="text-ink-secondary">{label}</dt>
        <dd className="text-right text-ink">{value}</dd>
      </div>
    )

  return (
    <>
      <Modal
        open
        size="lg"
        title={shipment.carrierName}
        onClose={onClose}
        closeOnEsc={!splitting && step === null}
        footer={
          <>
            {canRebook && <Button variant="outline" onClick={() => setStep('rebook')}>{t('shipments.rebook')}</Button>}
            {splittable && <Button variant="outline" onClick={() => setSplitting(true)}>{t('shipments.split.open')}</Button>}
            {cancellable && <Button variant="outline" className="text-danger-soft-text" onClick={() => setStep('cancel')}>{t('shipments.cancelAtCarrier')}</Button>}
            <Button onClick={onClose}>{t('common.close')}</Button>
          </>
        }
      >
        <div className="space-y-4">
          <div className="flex items-start justify-between">
            <div>
              <p className="font-mono text-xs text-ink-secondary">{shipment.trackingNumber ?? t('shipments.noTracking')}</p>
              {(shipment.parcelCount ?? 1) > 1 && <Badge variant="info">{t('shipments.split.badge', { n: shipment.parcelNo, count: shipment.parcelCount })}</Badge>}
            </div>
            <StatusBadge status={shipment.status} />
          </div>

          {shipment.reviewNote && <Alert variant="warning">{shipment.reviewNote}</Alert>}
          {shipment.bookingError && <Alert variant="danger">{shipment.bookingError}</Alert>}

          <dl className="divide-y divide-line">
            {row(t('shipments.recipient'), `${shipment.recipientName} · ${shipment.recipientPhone ?? ''}`)}
            {row(t('shipments.address'), [shipment.addressStreet, shipment.addressWard, shipment.addressDistrict, shipment.addressProvince].filter(Boolean).join(', '))}
            {row(t('shipments.weight'), `${shipment.totalWeight} kg`)}
            {row(t('shipments.cod'), shipment.codAmount > 0 ? money(shipment.codAmount) : t('shipments.prepaid'))}
            {row(t('shipments.declaredValue'), money(shipment.declaredValue))}
            {row(t('shipments.carrierFee'), shipment.carrierFee != null ? money(shipment.carrierFee) : null)}
            {row(t('shipments.carrierOrderCode'), shipment.carrierOrderCode)}
            {row(t('shipments.whyThisCarrier'), shipment.selectionNote)}
            {row(t('shipments.eta'), shipment.estimatedDelivery ? format(new Date(shipment.estimatedDelivery), 'dd MMM yyyy') : null)}
            {row(t('shipments.lastSync'), shipment.lastCarrierSyncAt ? format(new Date(shipment.lastCarrierSyncAt), 'dd MMM yyyy HH:mm') : null)}
          </dl>

          <div>
            <h3 className="mb-2 text-sm font-semibold text-ink">{t('shipments.timeline')}</h3>
            <ol className="space-y-2 border-l border-line pl-4">
              {shipment.trackingEvents.map((e, i) => (
                <li key={i} className="text-sm">
                  <div className="font-medium text-ink">{e.status}</div>
                  <div className="text-xs text-ink-secondary">{e.description} · {format(new Date(e.occurredAt), 'dd MMM HH:mm')}</div>
                </li>
              ))}
              {shipment.trackingEvents.length === 0 && <li className="text-sm text-ink-secondary">{t('shipments.noEvents')}</li>}
            </ol>
          </div>
        </div>
      </Modal>

      <ConfirmDialog
        open={step !== null}
        title={step === 'cancel' ? t('shipments.cancelTitle') : t('shipments.rebookTitle')}
        message={step === 'cancel' ? t('shipments.cancelConfirm', { carrier: shipment.carrierName }) : t('shipments.rebookConfirm')}
        confirmLabel={step === 'cancel' ? t('shipments.cancelAtCarrier') : t('shipments.rebookTitle')}
        danger={step === 'cancel'}
        onConfirm={() => (step === 'cancel' ? cancel.mutate() : rebook.mutate())}
        onCancel={() => setStep(null)}
      />

      {splitting && <SplitDialog shipment={shipment} onClose={() => setSplitting(false)} onDone={() => { refresh(); onClose() }} />}
    </>
  )
}

export function ShipmentsPage() {
  const { t } = useTranslation()
  const { page, pageSize, setPage } = usePagination()
  const [status, setStatus] = useState('')
  const [search, setSearch] = useState('')
  const [needsReview, setNeedsReview] = useState(false)
  const [selected, setSelected] = useState<Shipment | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['shipments', { page, pageSize, status, search, needsReview }],
    queryFn: () => shippingApi.listShipments({ page, pageSize, status: status || undefined, search: search || undefined, needsReview: needsReview || undefined }),
  })

  const filtering = !!(status || search || needsReview)
  const clear = () => {
    setStatus('')
    setSearch('')
    setNeedsReview(false)
    setPage(1)
  }

  const columns = [
    { key: 'tracking', label: t('shipments.tracking'), className: 'font-mono text-xs', render: (s: Shipment) => s.trackingNumber ?? '—' },
    { key: 'carrier', label: t('shipments.carrier'), render: (s: Shipment) => s.carrierName },
    { key: 'recipient', label: t('shipments.recipient'), render: (s: Shipment) => `${s.recipientName}, ${s.addressProvince}` },
    { key: 'cod', label: t('shipments.cod'), align: 'right' as const, render: (s: Shipment) => (s.codAmount > 0 ? money(s.codAmount) : '—') },
    { key: 'fee', label: t('shipments.carrierFee'), align: 'right' as const, render: (s: Shipment) => (s.carrierFee != null ? money(s.carrierFee) : '—') },
    {
      key: 'status',
      label: t('common.status'),
      render: (s: Shipment) => (
        <div className="space-y-1">
          <StatusBadge status={s.status} />
          {s.reviewNote && <div><Badge variant="warning">{t('shipments.needsAttention')}</Badge></div>}
        </div>
      ),
    },
    { key: 'createdAt', label: t('common.date'), render: (s: Shipment) => format(new Date(s.createdAt), 'dd MMM yyyy') },
    {
      key: 'actions',
      label: '',
      render: (s: Shipment) => (
        <Button variant="outline" size="sm" aria-label={`${t('common.view')} ${s.trackingNumber ?? s.carrierName}`} onClick={() => setSelected(s)}>
          {t('common.view')}
        </Button>
      ),
    },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('shipments.title')}</h1>

      <div className="flex flex-wrap items-center gap-3">
        <Input
          type="search"
          aria-label={t('shipments.searchPlaceholder')}
          className="w-64"
          placeholder={t('shipments.searchPlaceholder')}
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1) }}
        />
        <Select aria-label={t('common.status')} className="w-auto" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
          {STATUS_FILTERS.map((s) => <option key={s} value={s}>{s ? t(`shipments.status.${s}`) : t('common.all')}</option>)}
        </Select>
        <Checkbox label={t('shipments.needsAttention')} checked={needsReview} onChange={(e) => { setNeedsReview(e.target.checked); setPage(1) }} />
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        emptyState={
          filtering ? (
            <EmptyState variant="no-results" title={t('common.noResults')} action={<Button variant="outline" size="sm" onClick={clear}>{t('common.clear')}</Button>} />
          ) : (
            <EmptyState title={t('shipments.empty')} />
          )
        }
      />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.totalCount ?? 0} onPageChange={setPage} />

      {selected && <ShipmentDetail shipment={selected} onClose={() => setSelected(null)} />}
    </div>
  )
}
