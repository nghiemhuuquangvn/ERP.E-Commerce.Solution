import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useParams, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useForm } from 'react-hook-form'
import { inventoryApi } from '@/api/inventory.api'
import { stockAdjustmentsApi } from '@/api/stockAdjustments.api'
import type { AdjustmentReason } from '@/api/stockAdjustments.api'
import { usePermission } from '@/hooks/usePermission'
import { StockPolicyCard } from './StockPolicyCard'
import { format } from 'date-fns'

type Tab = 'locations' | 'lots' | 'serials' | 'costHistory'

interface AdjustmentFormValues {
  quantityDelta: number
  reason: AdjustmentReason
  notes: string
}

function RequestAdjustmentModal({ productId, onClose }: { productId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const { register, handleSubmit, formState: { errors } } = useForm<AdjustmentFormValues>({
    defaultValues: { quantityDelta: 0, reason: 'Correction', notes: '' },
  })

  const mutation = useMutation({
    mutationFn: (v: AdjustmentFormValues) =>
      stockAdjustmentsApi.request({
        productId,
        quantityDelta: Number(v.quantityDelta),
        reason: v.reason,
        notes: v.notes || undefined,
      }),
    onSuccess: onClose,
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-1 text-base font-semibold text-gray-900">{t('inventory.adjustment.requestTitle')}</h3>
        <p className="mb-4 text-xs text-gray-500">{t('inventory.adjustment.requestHint')}</p>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('inventory.adjustment.quantityDelta')}</label>
            <input
              type="number"
              className="input"
              {...register('quantityDelta', { required: true, valueAsNumber: true, validate: (v) => v !== 0 })}
            />
            {errors.quantityDelta && <p className="mt-1 text-xs text-red-600">{t('inventory.adjustment.quantityDeltaRequired')}</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('inventory.adjustment.reason')}</label>
            <select className="input" {...register('reason', { required: true })}>
              <option value="Damaged">{t('inventory.adjustment.reasons.damaged')}</option>
              <option value="Lost">{t('inventory.adjustment.reasons.lost')}</option>
              <option value="Correction">{t('inventory.adjustment.reasons.correction')}</option>
              <option value="Other">{t('inventory.adjustment.reasons.other')}</option>
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.notes')}</label>
            <textarea className="input" rows={2} {...register('notes')} />
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('common.failedToSave')}</p>}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('inventory.adjustment.submit')}
            </button>
          </div>
        </form>
      </div>
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

  const { data: lots } = useQuery({
    queryKey: ['inventory', productId, 'lots'],
    queryFn: () => inventoryApi.lotsByProduct(productId!),
    enabled: Boolean(productId) && tab === 'lots',
  })

  const { data: serials } = useQuery({
    queryKey: ['inventory', productId, 'serials'],
    queryFn: () => inventoryApi.serialsByProduct(productId!),
    enabled: Boolean(productId) && tab === 'serials',
  })

  const { data: costHistory } = useQuery({
    queryKey: ['inventory', productId, 'costHistory'],
    queryFn: () => inventoryApi.costConsumption(productId!),
    enabled: Boolean(productId) && tab === 'costHistory',
  })

  if (isLoading) return <div className="text-gray-400">{t('common.loading')}</div>
  if (!inventory) return <div className="text-gray-400">{t('inventory.notFound')}</div>

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/inventory" className="text-sm text-gray-500 hover:text-gray-700">
          {t('inventory.backToInventory')}
        </Link>
        <h1 className="text-xl font-semibold">{inventory.productName}</h1>
      </div>

      <div className="grid grid-cols-2 gap-6 md:grid-cols-4">
        <div className="card p-4">
          <p className="text-xs uppercase text-gray-400">{t('inventory.onHand')}</p>
          <p className="text-lg font-semibold">{inventory.totalQuantity}</p>
        </div>
        <div className="card p-4">
          <p className="text-xs uppercase text-gray-400">{t('inventory.available')}</p>
          <p className="text-lg font-semibold text-green-700">{inventory.availableQuantity}</p>
        </div>
        <div className="card p-4">
          <p className="text-xs uppercase text-gray-400">{t('inventory.reserved')}</p>
          <p className="text-lg font-semibold">{inventory.reservedQuantity}</p>
        </div>
        <div className="card p-4">
          <p className="text-xs uppercase text-gray-400">{t('inventory.reorderPoint')}</p>
          <p className="text-lg font-semibold">{inventory.reorderPoint}</p>
        </div>
      </div>

      <StockPolicyCard inventory={inventory} canEdit={hasPermission('inventory:manage')} />

      {hasPermission('inventory:adjustment:create') && (
        <button className="btn-outline" onClick={() => setModalOpen(true)}>
          {t('inventory.adjustment.request')}
        </button>
      )}

      <div className="flex gap-1 border-b border-gray-200">
        {(['locations', 'lots', 'serials', 'costHistory'] as Tab[]).map((tb) => (
          <button
            key={tb}
            onClick={() => setTab(tb)}
            className={`px-4 py-2 text-sm font-medium ${
              tab === tb ? 'border-b-2 border-primary-600 text-primary-700' : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            {t(`inventory.tabs.${tb}`)}
          </button>
        ))}
      </div>

      {tab === 'locations' && (
        <div className="card p-5">
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 text-left text-xs text-gray-400 uppercase">
                <th className="pb-2">{t('inventory.location')}</th>
                <th className="pb-2 text-right">{t('inventory.onHand')}</th>
                <th className="pb-2 text-right">{t('inventory.reserved')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {inventory.locations.map((loc) => (
                <tr key={loc.location}>
                  <td className="py-2 font-mono text-xs">{loc.location}</td>
                  <td className="py-2 text-right">{loc.quantity}</td>
                  <td className="py-2 text-right">{loc.reserved}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {inventory.locations.length === 1 && inventory.locations[0].location === 'DEFAULT_LOCATION' && (
            <p className="mt-3 text-xs text-gray-400">{t('inventory.noBinLevelData')}</p>
          )}
        </div>
      )}

      {tab === 'lots' && (
        <div className="card p-5">
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 text-left text-xs text-gray-400 uppercase">
                <th className="pb-2">{t('inventory.lotNumber')}</th>
                <th className="pb-2 text-right">{t('inventory.remaining')}</th>
                <th className="pb-2 text-right">{t('inventory.reserved')}</th>
                <th className="pb-2 text-right">{t('inventory.available')}</th>
                <th className="pb-2 text-right">{t('inventory.unitCost')}</th>
                <th className="pb-2">{t('inventory.expiryDate')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {(lots ?? []).map((lot) => (
                <tr key={lot.lotId}>
                  <td className="py-2 font-mono text-xs">{lot.lotNumber}</td>
                  <td className="py-2 text-right">{lot.remainingQuantity} / {lot.receivedQuantity}</td>
                  <td className="py-2 text-right">{lot.reservedQuantity}</td>
                  <td className="py-2 text-right text-green-700">{lot.availableQuantity}</td>
                  <td className="py-2 text-right">{lot.unitCost.toFixed(2)}</td>
                  <td className="py-2">{lot.expiryDate ? format(new Date(lot.expiryDate), 'dd MMM yyyy') : '—'}</td>
                </tr>
              ))}
              {(lots ?? []).length === 0 && (
                <tr><td colSpan={6} className="py-4 text-center text-gray-400">{t('common.noRecordsFound')}</td></tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {tab === 'costHistory' && (
        <div className="card p-5">
          <p className="mb-3 text-xs text-gray-500">{t('inventory.costHistory.hint')}</p>
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 text-left text-xs text-gray-400 uppercase">
                <th className="pb-2">{t('inventory.costHistory.consumedAt')}</th>
                <th className="pb-2">{t('inventory.costHistory.sourceOrder')}</th>
                <th className="pb-2">{t('inventory.lotNumber')}</th>
                <th className="pb-2 text-right">{t('inventory.costHistory.quantity')}</th>
                <th className="pb-2 text-right">{t('inventory.unitCost')}</th>
                <th className="pb-2 text-right">{t('inventory.costHistory.totalCost')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {(costHistory ?? []).map((log) => (
                <tr key={log.id}>
                  <td className="py-2">{format(new Date(log.consumedAt), 'dd MMM yyyy HH:mm')}</td>
                  <td className="py-2 font-mono text-xs">{log.sourceOrderId}</td>
                  <td className="py-2 font-mono text-xs">{log.lotId ?? t('inventory.costHistory.noLot')}</td>
                  <td className="py-2 text-right">{log.quantity}</td>
                  <td className="py-2 text-right">{log.unitCost.toFixed(2)}</td>
                  <td className="py-2 text-right font-medium">{log.totalCost.toFixed(2)}</td>
                </tr>
              ))}
              {(costHistory ?? []).length === 0 && (
                <tr><td colSpan={6} className="py-4 text-center text-gray-400">{t('common.noRecordsFound')}</td></tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {tab === 'serials' && (
        <div className="card p-5">
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 text-left text-xs text-gray-400 uppercase">
                <th className="pb-2">{t('inventory.serialNumber')}</th>
                <th className="pb-2">{t('common.status')}</th>
                <th className="pb-2">{t('common.date')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {(serials ?? []).map((s) => (
                <tr key={s.serialId}>
                  <td className="py-2 font-mono text-xs">{s.serialNumber}</td>
                  <td className="py-2">{s.status}</td>
                  <td className="py-2">{format(new Date(s.receivedDate), 'dd MMM yyyy')}</td>
                </tr>
              ))}
              {(serials ?? []).length === 0 && (
                <tr><td colSpan={3} className="py-4 text-center text-gray-400">{t('common.noRecordsFound')}</td></tr>
              )}
            </tbody>
          </table>
        </div>
      )}

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
