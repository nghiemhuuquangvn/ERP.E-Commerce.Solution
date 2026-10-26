import { useEffect, useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { inventoryApi } from '@/api/inventory.api'
import type { Inventory } from '@/api/inventory.api'
import { serverMessage } from '@/api/purchasing/procurement.api'

/**
 * Replenishment policy of one product (ROADMAP P1-9): when available stock plus goods on order is at or below the minimum,
 * Purchasing proposes an order that brings it up to the maximum. The safety stock is the part of the minimum kept for surprises.
 */
export function StockPolicyCard({ inventory, canEdit }: { inventory: Inventory; canEdit: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [min, setMin] = useState(inventory.minStock)
  const [max, setMax] = useState(inventory.maxStock)
  const [safety, setSafety] = useState(inventory.safetyStock)

  useEffect(() => {
    setMin(inventory.minStock)
    setMax(inventory.maxStock)
    setSafety(inventory.safetyStock)
  }, [inventory.minStock, inventory.maxStock, inventory.safetyStock])

  const mutation = useMutation({
    mutationFn: () => inventoryApi.updateStockPolicy(inventory.productId, { minStock: min, maxStock: max, safetyStock: safety }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['inventory'] }),
  })

  const valid = safety >= 0 && min >= safety && max > min
  const dirty = min !== inventory.minStock || max !== inventory.maxStock || safety !== inventory.safetyStock

  return (
    <div className="card space-y-3 p-5">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold text-gray-900">{t('inventory.policy.title')}</h2>
        {inventory.isLowStock && (
          <span className="rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-700">{t('inventory.policy.belowMinimum')}</span>
        )}
      </div>
      <p className="text-xs text-gray-500">{t('inventory.policy.hint')}</p>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        {([
          ['minStock', min, setMin],
          ['safetyStock', safety, setSafety],
          ['maxStock', max, setMax],
        ] as const).map(([key, value, setter]) => (
          <div key={key}>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t(`inventory.policy.${key}`)}</label>
            <input
              type="number"
              min={0}
              className="input"
              value={value}
              disabled={!canEdit}
              onChange={(e) => setter(Number(e.target.value))}
            />
          </div>
        ))}
      </div>
      {!valid && <p className="text-xs text-red-600">{t('inventory.policy.invalid')}</p>}
      {mutation.isError && <p className="text-xs text-red-600">{serverMessage(mutation.error, t('common.failedToSave'))}</p>}
      {canEdit && (
        <div className="flex justify-end">
          <button className="btn-primary" disabled={!valid || !dirty || mutation.isPending} onClick={() => mutation.mutate()}>
            {mutation.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      )}
    </div>
  )
}
