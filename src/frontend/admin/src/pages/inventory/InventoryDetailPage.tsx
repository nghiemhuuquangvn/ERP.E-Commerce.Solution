import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useParams, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useForm } from 'react-hook-form'
import { inventoryApi } from '@/api/inventory.api'
import { stockAdjustmentsApi } from '@/api/stockAdjustments.api'
import { ADJUSTMENT_REASON } from '@/api/stockAdjustments.api'
import type { AdjustmentReason } from '@/api/stockAdjustments.api'
import { DataTable } from '@/components/shared/DataTable'
import { usePermission } from '@/hooks/usePermission'
import { StockPolicyCard } from './StockPolicyCard'
import { format } from 'date-fns'
import { Alert, Button, EmptyState, FormField, Input, Modal, Select, Spinner, Textarea, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

type Tab = 'locations' | 'lots' | 'serials' | 'costHistory'
const TABS: Tab[] = ['locations', 'lots', 'serials', 'costHistory']

interface AdjustmentFormValues {
  quantityDelta: number
  reason: AdjustmentReason
  notes: string
}

function RequestAdjustmentModal({ productId, onClose }: { productId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const { register, handleSubmit, formState: { errors } } = useForm<AdjustmentFormValues>({
    defaultValues: { quantityDelta: 0, reason: 'Correction', notes: '' },
  })

  const mutation = useMutation({
    mutationFn: (v: AdjustmentFormValues) =>
      stockAdjustmentsApi.request({
        productId,
        quantityDelta: Number(v.quantityDelta),
        reason: ADJUSTMENT_REASON[v.reason],
        notes: v.notes || undefined,
      }),
    onSuccess: () => {
      toast.success(t('inventory.adjustment.requested'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={t('inventory.adjustment.requestTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="request-adjustment-form" loading={mutation.isPending}>{t('inventory.adjustment.submit')}</Button>
        </>
      }
    >
      <p className="mb-4 text-xs text-ink-secondary">{t('inventory.adjustment.requestHint')}</p>
      <form id="request-adjustment-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <FormField label={t('inventory.adjustment.quantityDelta')} required error={errors.quantityDelta ? t('common.required') : undefined}>
          <Input
            type="number"
            {...register('quantityDelta', { required: true, valueAsNumber: true, validate: (v) => v !== 0 })}
          />
        </FormField>
        <FormField label={t('inventory.adjustment.reason')} required error={errors.reason ? t('common.required') : undefined}>
          <Select {...register('reason', { required: true })}>
            <option value="Damaged">{t('inventory.adjustment.reasons.damaged')}</option>
            <option value="Lost">{t('inventory.adjustment.reasons.lost')}</option>
            <option value="Correction">{t('inventory.adjustment.reasons.correction')}</option>
            <option value="Other">{t('inventory.adjustment.reasons.other')}</option>
          </Select>
        </FormField>
        <FormField label={t('common.notes')}>
          <Textarea rows={2} {...register('notes')} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('common.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

function Kpi({ label, value, tone }: { label: string; value: number; tone?: 'success' }) {
  return (
    <div className="card p-4">
      <dt className="text-xs uppercase text-ink-secondary">{label}</dt>
      <dd className={`text-lg font-semibold tabular-nums ${tone === 'success' ? 'text-success-soft-text' : 'text-ink'}`}>{value}</dd>
    </div>
  )
}

export function InventoryDetailPage() {
  const { t } = useTranslation()
  const { productId } = useParams<{ productId: string }>()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [tab, setTab] = useState<Tab>('locations')
  const [modalOpen, setModalOpen] = useState(false)

  const { data: inventory, isLoading } = useQuery({
    queryKey: ['inventory', productId],
    queryFn: () => inventoryApi.get(productId!),
    enabled: Boolean(productId),
  })

  const lotsQuery = useQuery({
    queryKey: ['inventory', productId, 'lots'],
    queryFn: () => inventoryApi.lotsByProduct(productId!),
    enabled: Boolean(productId) && tab === 'lots',
  })

  const serialsQuery = useQuery({
    queryKey: ['inventory', productId, 'serials'],
    queryFn: () => inventoryApi.serialsByProduct(productId!),
    enabled: Boolean(productId) && tab === 'serials',
  })

  const costQuery = useQuery({
    queryKey: ['inventory', productId, 'costHistory'],
    queryFn: () => inventoryApi.costConsumption(productId!),
    enabled: Boolean(productId) && tab === 'costHistory',
  })

  if (isLoading) return <Spinner />
  if (!inventory) return <EmptyState variant="error" title={t('inventory.notFound')} />

  const right = 'right' as const
  const onlyDefaultLocation = inventory.locations.length === 1 && inventory.locations[0].location === 'DEFAULT_LOCATION'

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/inventory" className="text-sm text-ink-secondary hover:text-ink">
          {t('inventory.backToInventory')}
        </Link>
        <h1 className="text-xl font-semibold">{inventory.productName}</h1>
      </div>

      <dl className="grid grid-cols-2 gap-6 md:grid-cols-4">
        <Kpi label={t('inventory.onHand')} value={inventory.totalQuantity} />
        <Kpi label={t('inventory.available')} value={inventory.availableQuantity} tone="success" />
        <Kpi label={t('inventory.reserved')} value={inventory.reservedQuantity} />
        <Kpi label={t('inventory.reorderPoint')} value={inventory.reorderPoint} />
      </dl>

      <StockPolicyCard inventory={inventory} canEdit={hasPermission('inventory:manage')} />

      {hasPermission('inventory:adjustment:create') && (
        <Button variant="outline" onClick={() => setModalOpen(true)}>
          {t('inventory.adjustment.request')}
        </Button>
      )}

      <div role="tablist" className="flex gap-1 border-b border-line">
        {TABS.map((tb) => (
          <button
            key={tb}
            type="button"
            role="tab"
            id={`inv-tab-${tb}`}
            aria-selected={tab === tb}
            aria-controls="inv-tabpanel"
            onClick={() => setTab(tb)}
            className={`px-4 py-2 text-sm font-medium ${
              tab === tb ? 'border-b-2 border-brand-action text-brand-action' : 'text-ink-secondary hover:text-ink'
            }`}
          >
            {t(`inventory.tabs.${tb}`)}
          </button>
        ))}
      </div>

      <div id="inv-tabpanel" role="tabpanel" aria-labelledby={`inv-tab-${tab}`}>
        {tab === 'locations' && (
          <>
            <DataTable
              columns={[
                { key: 'location', label: t('inventory.location'), className: 'font-mono text-xs' },
                { key: 'quantity', label: t('inventory.onHand'), align: right },
                { key: 'reserved', label: t('inventory.reserved'), align: right },
              ]}
              data={inventory.locations.map((l) => ({ ...l, id: l.location }))}
              isLoading={false}
            />
            {onlyDefaultLocation && <p className="mt-3 text-xs text-ink-secondary">{t('inventory.noBinLevelData')}</p>}
          </>
        )}

        {tab === 'lots' && (
          <DataTable
            columns={[
              { key: 'lotNumber', label: t('inventory.lotNumber'), className: 'font-mono text-xs' },
              { key: 'remainingQuantity', label: t('inventory.remaining'), align: right, render: (l) => `${l.remainingQuantity} / ${l.receivedQuantity}` },
              { key: 'reservedQuantity', label: t('inventory.reserved'), align: right },
              { key: 'availableQuantity', label: t('inventory.available'), align: right, render: (l) => <span className="text-success-soft-text">{l.availableQuantity}</span> },
              { key: 'unitCost', label: t('inventory.unitCost'), align: right, render: (l) => l.unitCost.toFixed(2) },
              { key: 'expiryDate', label: t('inventory.expiryDate'), render: (l) => (l.expiryDate ? format(new Date(l.expiryDate), 'dd MMM yyyy') : '—') },
            ]}
            data={(lotsQuery.data ?? []).map((l) => ({ ...l, id: l.lotId }))}
            isLoading={lotsQuery.isLoading}
          />
        )}

        {tab === 'costHistory' && (
          <>
            <p className="mb-3 text-xs text-ink-secondary">{t('inventory.costHistory.hint')}</p>
            <DataTable
              columns={[
                { key: 'consumedAt', label: t('inventory.costHistory.consumedAt'), render: (l) => format(new Date(l.consumedAt), 'dd MMM yyyy HH:mm') },
                { key: 'sourceOrderId', label: t('inventory.costHistory.sourceOrder'), className: 'font-mono text-xs' },
                { key: 'lotId', label: t('inventory.lotNumber'), className: 'font-mono text-xs', render: (l) => l.lotId ?? t('inventory.costHistory.noLot') },
                { key: 'quantity', label: t('inventory.costHistory.quantity'), align: right },
                { key: 'unitCost', label: t('inventory.unitCost'), align: right, render: (l) => l.unitCost.toFixed(2) },
                { key: 'totalCost', label: t('inventory.costHistory.totalCost'), align: right, render: (l) => <span className="font-medium">{l.totalCost.toFixed(2)}</span> },
              ]}
              data={costQuery.data ?? []}
              isLoading={costQuery.isLoading}
            />
          </>
        )}

        {tab === 'serials' && (
          <DataTable
            columns={[
              { key: 'serialNumber', label: t('inventory.serialNumber'), className: 'font-mono text-xs' },
              { key: 'status', label: t('common.status') },
              { key: 'receivedDate', label: t('common.date'), render: (s) => format(new Date(s.receivedDate), 'dd MMM yyyy') },
            ]}
            data={(serialsQuery.data ?? []).map((s) => ({ ...s, id: s.serialId }))}
            isLoading={serialsQuery.isLoading}
          />
        )}
      </div>

      {modalOpen && (
        <RequestAdjustmentModal
          productId={productId!}
          onClose={() => {
            setModalOpen(false)
            queryClient.invalidateQueries({ queryKey: ['inventory', productId] })
          }}
        />
      )}
    </div>
  )
}
